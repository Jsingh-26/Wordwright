using System.Net;
using System.Text;

namespace Wordwright.Core.Tests;

/// <summary>
/// A tiny HTTP server for the downloader's tests: it serves one file from
/// memory and can be told to ignore ranges, stop part-way through, or answer
/// with a plain "no". Localhost only, so no firewall or admin rights are needed.
/// </summary>
internal sealed class TestHttpServer : IDisposable
{
    private const int Attempts = 10;

    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stallGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HttpListener _listener;

    public TestHttpServer()
        : this([])
    {
    }

    /// <summary>
    /// <paramref name="portsToTryFirst"/> exist so a test can force a port
    /// collision instead of waiting for a random one; the rest of the attempts
    /// are random, because port 0 is not allowed by <see cref="HttpListener"/>.
    /// </summary>
    internal TestHttpServer(IReadOnlyList<int> portsToTryFirst)
    {
        _listener = Listen(portsToTryFirst);
        _ = Task.Run(ServeAsync);
    }

    /// <summary>
    /// Starts a listener on the first free port. A <b>new</b> listener is built
    /// for every attempt: when <see cref="HttpListener.Start"/> throws it
    /// disposes the listener, so the object from a failed attempt can never be
    /// reused — touching it again throws <see cref="ObjectDisposedException"/>,
    /// which is what made downloader runs fail intermittently.
    /// </summary>
    private HttpListener Listen(IReadOnlyList<int> portsToTryFirst)
    {
        var ports = portsToTryFirst
            .Concat(Enumerable.Range(0, Attempts).Select(_ => Random.Shared.Next(20000, 60000)))
            .Take(Attempts);

        Exception? lastError = null;
        var tried = 0;

        foreach (var port in ports)
        {
            tried++;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                listener.Start();
                Url = $"http://localhost:{port}/model.gguf";
                return listener;
            }
            catch (Exception error)
            {
                lastError = error;
                Close(listener);
            }
        }

        throw new InvalidOperationException(
            $"could not start the test HTTP server after {tried} attempt(s).", lastError);
    }

    /// <summary>Closes a listener that may already have disposed itself.</summary>
    private static void Close(HttpListener listener)
    {
        try
        {
            listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // A failed Start() already disposed it; nothing left to release.
        }
    }

    public string Url { get; private set; } = "";

    /// <summary>What the server hands out.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>When false, the server always answers 200 with the whole file,
    /// the way a server without range support does.</summary>
    public bool SupportsRange { get; set; } = true;

    /// <summary>When set, the server closes the connection after this many bytes
    /// of the body, mid-response, the way a dropped connection does.</summary>
    public int StopAfterBytes { get; set; }

    /// <summary>
    /// When set, the server writes this many bytes of the body and then holds the
    /// response open until <see cref="ReleaseStall"/>. A test that cancels on a
    /// fixed delay races the transfer — on a fast machine 4 MB over localhost can
    /// finish first — so it can wait for <see cref="Stalled"/> instead and know
    /// the download is still in flight.
    /// </summary>
    public int StallAfterBytes { get; set; }

    /// <summary>Completes once a stalled response has reached its stall point.</summary>
    public Task Stalled => _stalled.Task;

    /// <summary>Lets a stalled response finish.</summary>
    public void ReleaseStall() => _stallGate.TrySetResult();

    /// <summary>When set, every request is answered with this status.</summary>
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public int Requests { get; private set; }

    /// <summary>The Range headers seen, so a test can prove a download resumed.</summary>
    public List<string?> RangeRequests { get; } = [];

    private async Task ServeAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stopping.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await AnswerAsync(context);
            }
            catch (Exception)
            {
                // The client hung up; the next request is the interesting one.
            }
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        Requests++;
        var range = context.Request.Headers["Range"];
        RangeRequests.Add(range);

        var content = Content;
        var offset = 0;

        if (Status != HttpStatusCode.OK)
        {
            context.Response.StatusCode = (int)Status;
            context.Response.Close();
            return;
        }

        if (SupportsRange && range is not null && range.StartsWith("bytes=", StringComparison.Ordinal))
        {
            var from = long.Parse(range[6..].TrimEnd('-'));
            if (from >= content.Length)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                context.Response.Close();
                return;
            }

            offset = (int)from;
            context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
            context.Response.Headers["Content-Range"] =
                $"bytes {offset}-{content.Length - 1}/{content.Length}";
        }

        var length = content.Length - offset;

        // The declared length stays the whole body even when the connection is
        // going to be cut, which is what makes it look like a dropped connection
        // rather than a short file.
        context.Response.ContentLength64 = length;

        var written = StopAfterBytes > 0 ? Math.Min(length, StopAfterBytes) : length;

        if (StallAfterBytes > 0)
        {
            written = Math.Min(written, StallAfterBytes);
        }

        await context.Response.OutputStream.WriteAsync(content.AsMemory(offset, written), _stopping.Token);
        await context.Response.OutputStream.FlushAsync(_stopping.Token);

        if (StallAfterBytes > 0)
        {
            // The body is provably half-sent, so the client is mid-download.
            _stalled.TrySetResult();

            try
            {
                await _stallGate.Task.WaitAsync(_stopping.Token);
            }
            catch (OperationCanceledException)
            {
                // The server is being disposed; let this response go.
                return;
            }

            await context.Response.OutputStream.WriteAsync(
                content.AsMemory(offset + written, length - written), _stopping.Token);
            await context.Response.OutputStream.FlushAsync(_stopping.Token);
        }

        // An abort mid-body is how a dropped connection looks to the client.
        if (StopAfterBytes > 0)
        {
            context.Response.Abort();
        }
        else
        {
            context.Response.Close();
        }
    }

    /// <summary>A small file whose bytes are predictable, plus its SHA-256.</summary>
    public static (byte[] Bytes, string Hash) DummyFile(int size)
    {
        var bytes = new byte[size];
        for (var index = 0; index < size; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return (bytes, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
    }

    public void Dispose()
    {
        // Release a stalled response first, so disposal cannot block on it.
        _stallGate.TrySetResult();
        _stopping.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }

        _stopping.Dispose();
    }

    public override string ToString() => new StringBuilder(Url).ToString();
}
