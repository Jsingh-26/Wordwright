namespace Wordwright.Core.Tests;

/// <summary>
/// Tests for the downloader's test fixture itself.
///
/// The fixture used to fail intermittently in full-suite runs with
/// "ObjectDisposedException: Cannot access a disposed object. Object name:
/// 'System.Net.HttpListener'" at the point where it picked the next port. That
/// cost real time to diagnose (GitHub issue #2), so the collision path is
/// pinned down here rather than left to luck.
/// </summary>
public class TestHttpServerTests
{
    /// <summary>
    /// A port that is already taken must be skipped, and the server must still
    /// come up on another one and serve.
    ///
    /// <see cref="System.Net.HttpListener.Start"/> disposes the listener when it
    /// throws, so a retry has to build a new one; touching the failed instance
    /// again threw ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task A_taken_port_is_skipped_and_the_server_still_serves()
    {
        using var first = new TestHttpServer();
        var taken = new Uri(first.Url).Port;

        // Told to try a port that is certainly held, so the retry runs for
        // certain instead of waiting for a random collision.
        using var second = new TestHttpServer([taken]);
        var (bytes, _) = TestHttpServer.DummyFile(256);
        second.Content = bytes;

        Assert.NotEqual(taken, new Uri(second.Url).Port);

        using var client = new HttpClient();
        var served = await client.GetByteArrayAsync(second.Url);

        Assert.Equal(bytes, served);
    }
}
