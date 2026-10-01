using System.Diagnostics;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Wordwright.Core.Hardware;

namespace Wordwright.Inference;

/// <summary>Where the model's weights live while it runs
/// (docs/ARCHITECTURE.md → Model lifecycle → Backend). There is no partial
/// offload in v1: the whole model runs on the GPU or the whole model runs on
/// the CPU.</summary>
public enum InferenceBackend
{
    /// <summary>Always available, and the fallback.</summary>
    Cpu,

    /// <summary>Used only when a GPU has memory for the whole model plus headroom.</summary>
    Vulkan,
}

/// <summary>How to load a model on this PC.</summary>
public sealed record LocalModelOptions
{
    /// <summary>The GGUF file, inside the models folder.</summary>
    public required string ModelPath { get; init; }

    public InferenceBackend Backend { get; init; } = InferenceBackend.Cpu;

    /// <summary>Physical cores − 1, at least 2 (docs/ARCHITECTURE.md → Model lifecycle).</summary>
    public int Threads { get; init; } = 2;

    /// <summary>Context 4,096 tokens in v1 (docs/ARCHITECTURE.md → Generation parameters).</summary>
    public uint ContextSize { get; init; } = 4096;

    /// <summary>The catalog entry's <c>promptHints.disableThinking</c>, appended to
    /// the user message for a model that thinks out loud before answering. Null or
    /// blank means the model has no such mode.</summary>
    public string? DisableThinkingHint { get; init; }

    /// <summary>Unload the model this long after the last rewrite, or null to keep
    /// it loaded (docs/ARCHITECTURE.md → Model lifecycle → Unload).</summary>
    public TimeSpan? UnloadAfterIdle { get; init; }
}

/// <summary>One rewrite to run: the messages, and the sampling settings.</summary>
public sealed record GenerationRequest
{
    public required string SystemMessage { get; init; }

    public required string UserMessage { get; init; }

    /// <summary>Cap on new tokens; the caller works out
    /// <c>min(2 × input tokens + 64, 1,536)</c> (docs/ARCHITECTURE.md).</summary>
    public int MaxTokens { get; init; } = 512;

    public float Temperature { get; init; } = 0.3f;

    public float TopP { get; init; } = 0.9f;

    public float RepeatPenalty { get; init; } = 1.05f;

    /// <summary>Stop sequences, beyond the model's own end-of-turn token.</summary>
    public IReadOnlyList<string> AntiPrompts { get; init; } = [];
}

/// <summary>What a rewrite produced, with the numbers calibration needs
/// (docs/ARCHITECTURE.md → Calibration).</summary>
public sealed record GenerationResult
{
    public required string Text { get; init; }

    /// <summary>Tokens in the rendered prompt, including the chat template.</summary>
    public required int PromptTokens { get; init; }

    public required int GeneratedTokens { get; init; }

    /// <summary>Seconds until the first token appeared — the prefill time.</summary>
    public required double PrefillSeconds { get; init; }

    /// <summary>Seconds spent generating after the first token.</summary>
    public required double GenerateSeconds { get; init; }
}

/// <summary>
/// The on-device model: loads a GGUF, answers one rewrite at a time, and unloads
/// when it has been idle (docs/ARCHITECTURE.md → Model lifecycle, P6.1). It is
/// the only place that touches LLamaSharp; everything above it deals in plain
/// strings.
/// <para>
/// Nothing here writes to a log or to disk, and no prompt or answer is kept
/// after the call returns (docs/AGENTS.md 2).
/// </para>
/// </summary>
public sealed class LocalModel : IDisposable
{
    /// <summary>Dedicated GPU memory the whole model needs beyond its own size
    /// before Vulkan is chosen (docs/ARCHITECTURE.md → Model lifecycle).</summary>
    private const ulong GpuHeadroomBytes = 1_000_000_000;

    private readonly object _gate = new();

    private LLamaWeights? _weights;
    private StatelessExecutor? _executor;
    private Timer? _idleTimer;
    private TimeSpan? _idleAfter;
    private string? _thinkingHint;
    private bool _disposed;

    /// <summary>True while a model is loaded and ready to generate.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_gate)
            {
                return _weights is not null;
            }
        }
    }

    /// <summary>The backend the loaded model is running on.</summary>
    public InferenceBackend Backend { get; private set; } = InferenceBackend.Cpu;

    /// <summary>When the last rewrite finished, or null if none has run yet.</summary>
    public DateTimeOffset? LastUsedUtc { get; private set; }

    /// <summary>
    /// Picks the backend for a model of <paramref name="modelSizeBytes"/> on this
    /// PC: Vulkan when a GPU has room for the whole file plus 1 GB, otherwise CPU
    /// (docs/ARCHITECTURE.md → Model lifecycle → Backend).
    /// </summary>
    public static InferenceBackend ChooseBackend(HardwareProfile profile, long modelSizeBytes)
    {
        var needed = (modelSizeBytes > 0 ? (ulong)modelSizeBytes : 0) + GpuHeadroomBytes;

        return profile.Gpus.Any(gpu => gpu.DedicatedMemoryBytes >= needed)
            ? InferenceBackend.Vulkan
            : InferenceBackend.Cpu;
    }

    /// <summary>Physical cores − 1, at least 2 (docs/ARCHITECTURE.md → Threads).</summary>
    public static int ThreadsFor(int physicalCores) => Math.Max(2, physicalCores - 1);

    /// <summary>
    /// Enables the Vulkan capability the first time a model is loaded. LLamaSharp
    /// only reads this before the native library opens, so it is set once, as
    /// early as possible, and never read again: a machine with a Vulkan-capable
    /// GPU then keeps the option open for any model it later loads. A machine
    /// without one uses the bundled CPU build.
    /// </summary>
    private static void EnsureNativeLibraryConfigured()
    {
        lock (NativeGate)
        {
            if (NativeConfigured)
            {
                return;
            }

            NativeConfigured = true;
            NativeLibraryConfig.LLama.WithVulkan(GpuCanRunVulkan());
        }
    }

    private static readonly object NativeGate = new();
    private static bool NativeConfigured;

    /// <summary>Whether the machine has any Vulkan-capable adapter. The catalogue
    /// recommendation already refused models the GPU cannot hold, so this only
    /// asks the broad question once.</summary>
    private static bool GpuCanRunVulkan()
    {
        try
        {
            return NativeApi.llama_supports_gpu_offload();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // No native library at all: the CPU build is all there is.
            Debug.WriteLine($"Vulkan support check failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads the model, reporting 0–1 progress. Loading a few gigabytes takes
    /// seconds, so the caller shows the "loading" pill while this runs.
    /// </summary>
    public async Task LoadAsync(
        LocalModelOptions options,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException("The model file is missing.", options.ModelPath);
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            UnloadCore();
            Backend = options.Backend;
            _idleAfter = options.UnloadAfterIdle;
            _thinkingHint = options.DisableThinkingHint;
        }

        EnsureNativeLibraryConfigured();

        var parameters = new ModelParams(options.ModelPath)
        {
            ContextSize = options.ContextSize,
            // Whether the GPU is used at all is decided per load; enabling the
            // Vulkan *capability* once (below) just makes that possible.
            GpuLayerCount = options.Backend == InferenceBackend.Vulkan ? int.MaxValue : 0,
            Threads = options.Threads,
        };

        LLamaWeights weights;
        try
        {
            weights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken, progress)
                .ConfigureAwait(false);
        }
        catch
        {
            // A load that failed leaves nothing behind.
            lock (_gate)
            {
                ClearModel();
            }

            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            weights.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var executor = new StatelessExecutor(weights, parameters);

        lock (_gate)
        {
            if (_disposed)
            {
                weights.Dispose();
                throw new ObjectDisposedException(nameof(LocalModel));
            }

            _weights = weights;
            _executor = executor;
        }
    }

    /// <summary>
    /// Runs one rewrite through the model's own chat template, with the
    /// thinking-off hint added when the catalog gave one. The call stops as soon
    /// as <paramref name="cancellationToken"/> fires.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        StatelessExecutor executor;
        LLamaWeights weights;
        string? thinkingHint;

        lock (_gate)
        {
            ThrowIfDisposed();
            executor = _executor ?? throw new InvalidOperationException("No model is loaded.");
            weights = _weights ?? throw new InvalidOperationException("No model is loaded.");
            thinkingHint = _thinkingHint;
        }

        var user = string.IsNullOrWhiteSpace(thinkingHint)
            ? request.UserMessage
            : request.UserMessage + "\n" + thinkingHint;

        var prompt = BuildChatPrompt(weights, request.SystemMessage, user);

        var inference = new InferenceParams
        {
            MaxTokens = request.MaxTokens,
            AntiPrompts = [.. request.AntiPrompts],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                RepeatPenalty = request.RepeatPenalty,
            },
        };

        var watch = Stopwatch.StartNew();
        double? prefillSeconds = null;
        var builder = new StringBuilder();

        try
        {
            await foreach (var chunk in executor.InferAsync(prompt, inference, cancellationToken)
                .ConfigureAwait(false))
            {
                prefillSeconds ??= watch.Elapsed.TotalSeconds;
                builder.Append(chunk);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled mid-rewrite: the caller shows "Cancelled", nothing is kept.
            MarkUsed();
            throw;
        }

        // StatelessExecutor stops its loop on cancellation rather than throwing,
        // so a cancelled run surfaces here instead of looking like a result.
        if (cancellationToken.IsCancellationRequested)
        {
            MarkUsed();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var total = watch.Elapsed.TotalSeconds;
        var text = builder.ToString();

        MarkUsed();

        return new GenerationResult
        {
            Text = text,
            PromptTokens = CountTokens(prompt),
            GeneratedTokens = CountTokens(text),
            PrefillSeconds = prefillSeconds ?? total,
            GenerateSeconds = Math.Max(0, total - (prefillSeconds ?? total)),
        };
    }

    /// <summary>How many tokens a piece of text is, for the "too long" limit and
    /// for the max-new-tokens rule.</summary>
    public int CountTokens(string text)
    {
        lock (_gate)
        {
            return _weights?.Tokenize(text, add_bos: false, special: false, LLamaTemplate.Encoding).Count() ?? 0;
        }
    }

    /// <summary>Unloads the model now, freeing its memory.</summary>
    public void Unload()
    {
        lock (_gate)
        {
            UnloadCore();
        }
    }

    /// <summary>Records that the model was just used and restarts the idle clock.</summary>
    private void MarkUsed()
    {
        lock (_gate)
        {
            LastUsedUtc = DateTimeOffset.UtcNow;

            if (_idleAfter is { } after && _weights is not null)
            {
                _idleTimer ??= new Timer(_ => OnIdle(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _idleTimer.Change(after, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>Runs on a timer thread: unload only if nothing has used the model
    /// since the timer was armed.</summary>
    private void OnIdle()
    {
        lock (_gate)
        {
            if (_weights is null || _idleAfter is not { } after)
            {
                return;
            }

            if (DateTimeOffset.UtcNow - (LastUsedUtc ?? DateTimeOffset.MinValue) < after)
            {
                // A rewrite finished while this callback waited for the lock, so
                // the idle clock has already been reset.
                return;
            }

            UnloadCore();
            Debug.WriteLine("LocalModel unloaded after idle timeout");
        }
    }

    /// <summary>
    /// Renders the system and user turns through the model's own chat template,
    /// with the assistant turn started so the model answers rather than continues
    /// the user. The same rendering feeds the token count, so the "too long"
    /// limit measures what the model actually sees (docs/ARCHITECTURE.md → Prompt).
    /// </summary>
    private static string BuildChatPrompt(LLamaWeights weights, string system, string user)
    {
        var template = new LLamaTemplate(weights)
        {
            AddAssistant = true,
        };

        template.Add("system", system);
        template.Add("user", user);

        return LLamaTemplate.Encoding.GetString(template.Apply());
    }

    private void UnloadCore()
    {
        _idleTimer?.Dispose();
        _idleTimer = null;
        ClearModel();
    }

    private void ClearModel()
    {
        _executor = null;

        _weights?.Dispose();
        _weights = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LocalModel));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnloadCore();
        }
    }
}
