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
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();

    public TestHttpServer()
    {
        // Port 0 is not allowed by HttpListener, so pick one at random and retry
        // the few times the port is taken.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var port = Random.Shared.Next(20000, 60000);
            Url = $"http://localhost:{port}/model.gguf";
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                _listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < 9)
            {
                // Taken; try another port.
            }
        }

        _ = Task.Run(ServeAsync);
    }

    public string Url { get; }

    /// <summary>What the server hands out.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>When false, the server always answers 200 with the whole file,
    /// the way a server without range support does.</summary>
    public bool SupportsRange { get; set; } = true;

    /// <summary>When set, the server closes the connection after this many bytes
    /// of the body, mid-response, the way a dropped connection does.</summary>
    public int StopAfterBytes { get; set; }

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
        await context.Response.OutputStream.WriteAsync(content.AsMemory(offset, written), _stopping.Token);
        await context.Response.OutputStream.FlushAsync(_stopping.Token);

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
