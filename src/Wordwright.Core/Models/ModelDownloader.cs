using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Wordwright.Core.Models;

/// <summary>How far a download has got.</summary>
public sealed record DownloadProgress(long BytesDone, long TotalBytes)
{
    /// <summary>0 to 1, or 0 when the size is not known.</summary>
    public double Fraction => TotalBytes > 0 ? BytesDone / (double)TotalBytes : 0;
}

/// <summary>Why a download did not end with a verified file.</summary>
public enum DownloadFailure
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The catalog entry has no SHA-256, so nothing can be verified.
    /// Wordwright never downloads a file it cannot check (docs/AGENTS.md 5).</summary>
    NoHash,

    /// <summary>The disk cannot hold the file plus room to work in.</summary>
    NotEnoughDisk,

    /// <summary>The download stopped: no connection, or the server went away.
    /// A partial file is kept so the next attempt can resume.</summary>
    Stopped,

    /// <summary>The file arrived but was not what the catalog said. It has been
    /// deleted.</summary>
    Corrupt,
}

/// <summary>
/// Fetches a model file (docs/MODELS.md, docs/ARCHITECTURE.md → Catalog,
/// recommendation, download). It downloads into <c>&lt;file&gt;.part</c> and only
/// renames it into place once the SHA-256 matches, so a half-downloaded or
/// tampered file can never look installed. An interrupted download leaves the
/// part file behind, and the next call resumes from where it stopped.
/// </summary>
public sealed class ModelDownloader
{
    /// <summary>Free disk the download needs, as a multiple of the file size.</summary>
    private const double DiskHeadroomFactor = 1.2;

    private readonly HttpClient _http;
    private readonly Func<string, long> _availableSpace;

    /// <param name="http">The client to fetch with; the app passes one with no
    /// proxy surprises, tests pass one pointed at their own server.</param>
    /// <param name="availableSpace">Free bytes on the drive holding a directory.
    /// Injectable so the disk rule can be tested without filling a disk.</param>
    public ModelDownloader(HttpClient? http = null, Func<string, long>? availableSpace = null)
    {
        _http = http ?? new HttpClient();
        _availableSpace = availableSpace ?? FreeSpace;
    }

    /// <summary>The file name a model is stored under.</summary>
    public static string FileNameFor(CatalogEntry model) => model.Id + ".gguf";

    /// <summary>
    /// Downloads <paramref name="model"/> into <paramref name="directory"/>.
    /// Throws <see cref="OperationCanceledException"/> when cancelled; every
    /// other way of not finishing comes back as a <see cref="DownloadFailure"/>.
    /// </summary>
    public async Task<DownloadFailure> DownloadAsync(
        CatalogEntry model,
        string directory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Sha256))
        {
            // Rule 5: an unverifiable download is not made at all.
            return DownloadFailure.NoHash;
        }

        if (string.IsNullOrWhiteSpace(model.Source.Url))
        {
            return DownloadFailure.Stopped;
        }

        Directory.CreateDirectory(directory);
        var finalPath = Path.Combine(directory, FileNameFor(model));
        var partPath = finalPath + ".part";

        // The disk has to hold the file and leave room to work in.
        var needed = (long)(model.SizeBytes * DiskHeadroomFactor);
        if (needed > 0 && _availableSpace(directory) < needed)
        {
            return DownloadFailure.NotEnoughDisk;
        }

        var resumeFrom = PartLength(partPath);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, model.Source.Url);
            if (resumeFrom > 0)
            {
                request.Headers.Range = new RangeHeaderValue(resumeFrom, null);
            }

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (resumeFrom > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // Nothing left to fetch: whatever is on disk is the whole file.
                response.Dispose();
                return Finish(partPath, finalPath, model.Sha256);
            }

            if (!response.IsSuccessStatusCode)
            {
                return DownloadFailure.Stopped;
            }

            // A server that ignores the range starts us over rather than gluing
            // the answer onto the wrong end of the part file.
            var appending = resumeFrom > 0 && response.StatusCode == HttpStatusCode.PartialContent;
            if (!appending)
            {
                resumeFrom = 0;
            }

            var total = (response.Content.Headers.ContentLength ?? 0) + resumeFrom;
            await WriteAsync(response, partPath, appending, resumeFrom, total, progress, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return DownloadFailure.Stopped;
        }
        catch (IOException)
        {
            return DownloadFailure.Stopped;
        }

        return Finish(partPath, finalPath, model.Sha256);
    }

    /// <summary>Checks the whole file against the catalog and puts it in place.</summary>
    private static DownloadFailure Finish(string partPath, string finalPath, string sha256)
    {
        if (!File.Exists(partPath))
        {
            return DownloadFailure.Stopped;
        }

        if (!string.Equals(Hash(partPath), sha256, StringComparison.OrdinalIgnoreCase))
        {
            // A file that does not match is not kept at all (docs/AGENTS.md 5).
            File.Delete(partPath);
            return DownloadFailure.Corrupt;
        }

        File.Move(partPath, finalPath, overwrite: true);

        return DownloadFailure.None;
    }

    private static async Task WriteAsync(
        HttpResponseMessage response,
        string partPath,
        bool appending,
        long resumeFrom,
        long total,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var mode = appending ? FileMode.Append : FileMode.Create;

        await using var file = new FileStream(partPath, mode, FileAccess.Write, FileShare.None);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[81920];
        var done = appending ? resumeFrom : 0;
        progress?.Report(new DownloadProgress(done, total));

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            done += read;
            progress?.Report(new DownloadProgress(done, total));
        }

        await file.FlushAsync(cancellationToken);
    }

    /// <summary>The whole-file hash, for the downloader and for the import path.</summary>
    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static long PartLength(string partPath) =>
        File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

    private static long FreeSpace(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));

        return root is null ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
    }
}
