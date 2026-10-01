using System.IO;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Models;

/// <summary>
/// One model this PC has: downloaded from the catalog, or imported by the user
/// (docs/ARCHITECTURE.md → installed-models.json).
/// </summary>
public sealed record InstalledModel
{
    /// <summary>The catalog id, or a name made from the file for a custom import.</summary>
    public required string Id { get; init; }

    /// <summary>The file name inside the models folder.</summary>
    public required string File { get; init; }

    public required string Sha256 { get; init; }

    /// <summary>True when the file matched a catalog entry's hash; false for a
    /// custom import, which has no estimate until it is calibrated.</summary>
    public required bool Verified { get; init; }

    public DateTimeOffset InstalledUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Measured speed on this PC, once it has been calibrated (P7.1).</summary>
    public ModelCalibration? Calibration { get; init; }
}

/// <summary>What calibration measured (docs/ARCHITECTURE.md → Calibration).</summary>
public sealed record ModelCalibration
{
    public required double PromptTokensPerSecond { get; init; }

    public required double GenTokensPerSecond { get; init; }

    public required double LoadSeconds { get; init; }

    /// <summary>"cpu" or "vulkan".</summary>
    public required string Backend { get; init; }

    public DateTimeOffset MeasuredUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>The whole file.</summary>
public sealed record InstalledModelRegistry
{
    public int SchemaVersion { get; init; } = InstalledModelStore.SchemaVersion;

    public IReadOnlyList<InstalledModel> Models { get; init; } = [];
}

/// <summary>
/// Reads and writes <c>installed-models.json</c> under the models folder. A
/// corrupt file is renamed <c>.corrupt</c> and an empty list is used, so a bad
/// write can never leave the app unable to start.
/// </summary>
public sealed class InstalledModelStore
{
    public const int SchemaVersion = 1;

    internal const string FileName = "installed-models.json";

    private readonly string _path;

    /// <param name="directory">The models folder, in the app
    /// <c>%LocalAppData%\Wordwright\models</c>.</param>
    public InstalledModelStore(string directory)
    {
        DirectoryPath = directory;
        _path = Path.Combine(directory, FileName);
    }

    public string DirectoryPath { get; }

    public InstalledModelRegistry Load() =>
        JsonFile.Read<InstalledModelRegistry>(_path) ?? new InstalledModelRegistry();

    public void Save(InstalledModelRegistry registry) =>
        JsonFile.Write(_path, registry, keepBackup: false);

    /// <summary>Adds a model, replacing any record with the same id.</summary>
    public InstalledModelRegistry Add(InstalledModel model)
    {
        var registry = Load();
        var models = registry.Models.Where(existing => existing.Id != model.Id).ToList();
        models.Add(model);

        var updated = registry with { Models = models };
        Save(updated);

        return updated;
    }

    /// <summary>The full path of an installed model's file.</summary>
    public string PathOf(InstalledModel model) => Path.Combine(DirectoryPath, model.File);
}
