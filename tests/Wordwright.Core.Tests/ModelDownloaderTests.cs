using FluentAssertions;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

public class ModelDownloaderTests : IDisposable
{
    private readonly TestHttpServer _server = new();
    private readonly string _directory;
    private readonly HttpClient _http = new();

    public ModelDownloaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ModelDownloader Downloader(long freeSpace = long.MaxValue) =>
        new(_http, _ => freeSpace);

    private CatalogEntry Model(byte[] content, string hash, long sizeBytes = 0) => new()
    {
        Id = "tiny",
        Sha256 = hash,
        SizeBytes = sizeBytes,
        Source = new CatalogSource { Url = _server.Url },
    };

    private string FinalPath => Path.Combine(_directory, ModelDownloader.FileNameFor(new CatalogEntry { Id = "tiny" }));

    [Fact]
    public async Task ADownloadThatVerifies_isPutInPlace()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(200_000);
        _server.Content = bytes;

        var result = await Downloader().DownloadAsync(Model(bytes, hash), _directory);

        result.Should().Be(DownloadFailure.None);
        File.ReadAllBytes(FinalPath).Should().Equal(bytes);
        File.Exists(FinalPath + ".part").Should().BeFalse("the part file becomes the real file");
    }

    [Fact]
    public async Task AnEntryWithoutAHash_isNeverFetched()
    {
        var (bytes, _) = TestHttpServer.DummyFile(1000);
        _server.Content = bytes;

        var result = await Downloader().DownloadAsync(Model(bytes, ""), _directory);

        result.Should().Be(DownloadFailure.NoHash);
        _server.Requests.Should().Be(0, "an unverifiable download must not even start");
    }

    [Fact]
    public async Task ADiskTooSmallToHoldIt_isRefusedBeforeAnythingIsFetched()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(1000);
        _server.Content = bytes;

        // The file needs 1.2 times its size free.
        var result = await Downloader(freeSpace: 500).DownloadAsync(Model(bytes, hash, sizeBytes: 1000), _directory);

        result.Should().Be(DownloadFailure.NotEnoughDisk);
        _server.Requests.Should().Be(0);
    }

    [Fact]
    public async Task AFileThatDoesNotMatchItsHash_isDeleted()
    {
        var (bytes, _) = TestHttpServer.DummyFile(10_000);
        _server.Content = bytes;

        var result = await Downloader().DownloadAsync(Model(bytes, new string('a', 64)), _directory);

        result.Should().Be(DownloadFailure.Corrupt);
        File.Exists(FinalPath).Should().BeFalse();
        File.Exists(FinalPath + ".part").Should().BeFalse("a bad file is not left behind to resume from");
    }

    [Fact]
    public async Task AnInterruptedDownload_keepsWhatArrivedSoTheNextOneCanResume()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(200_000);
        _server.Content = bytes;
        _server.StopAfterBytes = 50_000;

        var stopped = await Downloader().DownloadAsync(Model(bytes, hash), _directory);

        stopped.Should().Be(DownloadFailure.Stopped);
        new FileInfo(FinalPath + ".part").Length.Should().BeLessThan(bytes.Length);
        File.Exists(FinalPath).Should().BeFalse();
    }

    [Fact]
    public async Task TheNextAttempt_asksForTheRestAndFinishesTheFile()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(200_000);
        _server.Content = bytes;

        // Pretend the first 120,000 bytes arrived last time.
        var partPath = FinalPath + ".part";
        await File.WriteAllBytesAsync(partPath, bytes[..120_000]);

        var result = await Downloader().DownloadAsync(Model(bytes, hash), _directory);

        result.Should().Be(DownloadFailure.None);
        _server.RangeRequests.Should().Contain("bytes=120000-");
        File.ReadAllBytes(FinalPath).Should().Equal(bytes);
    }

    [Fact]
    public async Task AServerWithoutRanges_startsTheFileAgain()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(120_000);
        _server.Content = bytes;
        _server.SupportsRange = false;

        // A part file of the wrong length would corrupt the result if it were appended to.
        await File.WriteAllBytesAsync(FinalPath + ".part", [1, 2, 3]);

        var result = await Downloader().DownloadAsync(Model(bytes, hash), _directory);

        result.Should().Be(DownloadFailure.None);
        File.ReadAllBytes(FinalPath).Should().Equal(bytes);
    }

    [Fact]
    public async Task AFinishedPartFile_isJustVerified()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(50_000);
        _server.Content = bytes;
        await File.WriteAllBytesAsync(FinalPath + ".part", bytes);

        var result = await Downloader().DownloadAsync(Model(bytes, hash), _directory);

        result.Should().Be(DownloadFailure.None);
        File.ReadAllBytes(FinalPath).Should().Equal(bytes);
    }

    [Fact]
    public async Task AServerThatRefuses_comesBackAsStopped()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(1000);
        _server.Content = bytes;
        _server.Status = System.Net.HttpStatusCode.InternalServerError;

        (await Downloader().DownloadAsync(Model(bytes, hash), _directory)).Should().Be(DownloadFailure.Stopped);
    }

    [Fact]
    public async Task Progress_isReportedToTheEnd()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(150_000);
        _server.Content = bytes;

        var seen = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(seen.Add);

        await Downloader().DownloadAsync(Model(bytes, hash), _directory, progress);

        // Progress is posted to the capturing context, so give it a moment.
        await Task.Delay(50);

        seen.Should().NotBeEmpty();
        seen.Should().OnlyContain(entry => entry.Fraction >= 0 && entry.Fraction <= 1);
        seen.Max(entry => entry.BytesDone).Should().Be(bytes.Length);
    }

    [Fact]
    public async Task Cancelling_stopsTheDownloadAndKeepsThePart()
    {
        var (bytes, hash) = TestHttpServer.DummyFile(4_000_000);
        _server.Content = bytes;

        // Hold the body open and cancel at the stall point. Cancelling after a
        // fixed delay raced the transfer: 4 MB over localhost can arrive inside
        // it on a fast machine, and then nothing was cancelled to observe.
        _server.StallAfterBytes = 64 * 1024;
        using var cancellation = new CancellationTokenSource();

        var download = Downloader().DownloadAsync(Model(bytes, hash), _directory, cancellationToken: cancellation.Token);
        await _server.Stalled.WaitAsync(TimeSpan.FromSeconds(15));
        cancellation.Cancel();

        await FluentActions.Awaiting(() => download).Should().ThrowAsync<OperationCanceledException>();
        File.Exists(FinalPath).Should().BeFalse();
    }

    [Fact]
    public void TheFileNameForAModel_isItsId()
    {
        ModelDownloader.FileNameFor(new CatalogEntry { Id = "qwen3.5-2b-q4km" })
            .Should().Be("qwen3.5-2b-q4km.gguf");
    }
}
