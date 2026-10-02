using System.IO;
using Wordwright.Core.Actions;
using Wordwright.Core.Models;
using Wordwright.Inference;
using Wordwright.Platform.Hardware;

namespace Wordwright.App.Ai;

/// <summary>Why a rewrite did not produce text to paste.</summary>
internal enum RewriteStatus
{
    /// <summary>It worked; <see cref="RewriteOutcome.Text"/> holds the rewrite.</summary>
    Done,

    /// <summary>AI is off, or no model file is installed.</summary>
    NoModel,

    /// <summary>This PC is too short of memory or disk to load the model now
    /// (docs/PLAN.md → "Maintainer requirement: startup resource eligibility").</summary>
    NotEnoughResources,

    /// <summary>The selection was past the v1 input limit.</summary>
    TooLong,

    /// <summary>The model's answer was not usable as a rewrite.</summary>
    BadOutput,
}

/// <summary>What a rewrite produced.</summary>
internal sealed record RewriteOutcome(RewriteStatus Status, string Text = "")
{
    public bool Succeeded => Status == RewriteStatus.Done;
}

/// <summary>
/// Owns the one on-device model and runs rewrites through it: load on first use
/// (the pill shows "loading"), generate, clean the answer
/// (docs/ARCHITECTURE.md → AI rewrite flow). The model unloads itself when idle.
/// <para>
/// The caller keeps a rewrite's text only as long as it needs it; nothing here
/// writes prompts or answers to disk or to a log (docs/AGENTS.md 2).
/// </para>
/// </summary>
internal sealed class RewriteService : IDisposable
{
    private readonly App _app;
    private readonly LocalModel _model = new();
    private readonly InstalledModelStore _installed;

    public RewriteService(App app)
    {
        _app = app;
        _installed = new InstalledModelStore(app.ModelsFolder);
    }

    /// <summary>True once the model is loaded and ready.</summary>
    public bool IsLoaded => _model.IsLoaded;

    /// <summary>The rewrite on this PC can use, or null when AI is off or nothing
    /// is installed.</summary>
    public InstalledModel? ActiveModel()
    {
        if (!_app.Settings.AiEnabled)
        {
            return null;
        }

        var models = _installed.Load().Models;
        if (models.Count == 0)
        {
            return null;
        }

        return models.FirstOrDefault(model => model.Id == _app.Settings.ActiveModelId) ?? models[0];
    }

    /// <summary>Unloads the model now, freeing its memory.</summary>
    public void Unload() => _model.Unload();

    /// <summary>Runs <paramref name="instruction"/> over <paramref name="text"/> and returns
    /// the cleaned rewrite. A cancellation stops generation and is reported as
    /// cancelled by the caller, not as a failure.</summary>
    public async Task<RewriteOutcome> RewriteAsync(
        string instruction,
        string text,
        Action? generationStarted = null,
        IProgress<float>? loadingProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (ActiveModel() is null)
        {
            return new RewriteOutcome(RewriteStatus.NoModel);
        }

        // Loading is the moment the memory is actually claimed, so the machine is
        // measured again here rather than trusting the startup result, and the
        // model is judged as itself: a PC that could only carry the smallest
        // catalog entry must not be allowed to load a larger one, and an imported
        // file the catalog does not know is judged on the strictest entry the
        // catalog has for a PC this size (docs/PLAN.md → resource eligibility).
        // A model that is already loaded keeps working — its memory is committed.
        if (!IsLoaded)
        {
            var active = ActiveModel()!;
            var entry = CatalogParser.Embedded().Models.FirstOrDefault(model => model.Id == active.Id);

            if (!(await AiGate.CheckForAsync(entry, cancellationToken).ConfigureAwait(false)).IsAvailable)
            {
                return new RewriteOutcome(RewriteStatus.NotEnoughResources);
            }
        }

        try
        {
            await EnsureLoadedAsync(loadingProgress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A model that will not load is, for the user, no model at all.
            System.Diagnostics.Debug.WriteLine($"model load failed: {exception.GetType().Name}");
            return new RewriteOutcome(RewriteStatus.NoModel);
        }

        generationStarted?.Invoke();

        if (_model.CountTokens(text) > PromptBuilder.MaxInputTokens)
        {
            return new RewriteOutcome(RewriteStatus.TooLong);
        }

        var messages = PromptBuilder.Build(instruction, text);

        var result = await _model.GenerateAsync(
            new GenerationRequest
            {
                SystemMessage = messages.System,
                UserMessage = messages.User,
                MaxTokens = PromptBuilder.MaxNewTokens(_model.CountTokens(text)),
                Temperature = PromptBuilder.Temperature,
                TopP = PromptBuilder.TopP,
                RepeatPenalty = PromptBuilder.RepeatPenalty,
            },
            cancellationToken).ConfigureAwait(false);

        var cleaned = OutputCleaner.Clean(text, result.Text);

        return cleaned.Accepted
            ? new RewriteOutcome(RewriteStatus.Done, cleaned.Text)
            : new RewriteOutcome(RewriteStatus.BadOutput);
    }

    /// <summary>Loads the active model if it is not already loaded, choosing the
    /// backend from this PC's hardware.</summary>
    public async Task EnsureLoadedAsync(
        IProgress<float>? progress,
        CancellationToken cancellationToken)
    {
        if (_model.IsLoaded)
        {
            return;
        }

        var active = ActiveModel() ?? throw new InvalidOperationException("No model is available.");
        var path = _installed.PathOf(active);

        var profile = await Task.Run(HardwareProbe.Read, cancellationToken).ConfigureAwait(false);

        var catalogEntry = CatalogParser.Embedded().Models
            .FirstOrDefault(entry => entry.Id == active.Id);

        await _model.LoadAsync(
            new LocalModelOptions
            {
                ModelPath = path,
                Backend = LocalModel.ChooseBackend(profile, new FileInfo(path).Length),
                Threads = LocalModel.ThreadsFor(profile.PhysicalCores),
                DisableThinkingHint = catalogEntry?.PromptHints.DisableThinking,
                UnloadAfterIdle = TimeSpan.FromMinutes(_app.Settings.UnloadAfterIdleMinutes),
            },
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _model.Dispose();
}
