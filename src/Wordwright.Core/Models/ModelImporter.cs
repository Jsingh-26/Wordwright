using System.IO;
using System.Text;

namespace Wordwright.Core.Models;

/// <summary>What came of importing a file the user picked.</summary>
public enum ImportOutcome
{
    /// <summary>It is in the models folder and registered.</summary>
    Imported,

    /// <summary>The file is not a GGUF model at all.</summary>
    NotGguf,

    /// <summary>The file could not be read or moved.</summary>
    Failed,
}

/// <summary>
/// Takes a model file the user picked and makes it usable
/// (docs/ARCHITECTURE.md → Import model file). The file is moved into the models
/// folder; if its SHA-256 matches a catalog entry it counts as that entry, and
/// otherwise it is registered as a custom, unverified model with no estimate
/// until calibration has measured it on this PC.
/// </summary>
public static class ModelImporter
{
    /// <summary>The four bytes every GGUF file starts with.</summary>
    private static readonly byte[] Magic = "GGUF"u8.ToArray();

    /// <summary>Whether the file starts with the GGUF magic.</summary>
    public static bool IsGguf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);

            var head = new byte[Magic.Length];
            return stream.Read(head, 0, head.Length) == head.Length && head.SequenceEqual(Magic);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Imports <paramref name="sourcePath"/> into <paramref name="modelsFolder"/>.
    /// </summary>
    public static (ImportOutcome Outcome, InstalledModel? Model, CatalogEntry? CatalogEntry) Import(
        string sourcePath,
        string modelsFolder,
        ModelCatalog catalog)
    {
        if (!File.Exists(sourcePath))
        {
            return (ImportOutcome.Failed, null, null);
        }

        if (!IsGguf(sourcePath))
        {
            return (ImportOutcome.NotGguf, null, null);
        }

        Directory.CreateDirectory(modelsFolder);

        var fileName = Path.GetFileName(sourcePath);
        var destination = Path.Combine(modelsFolder, fileName);

        try
        {
            if (!string.Equals(
                    Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                // Move it in: the file is Wordwright's model now, so "remove
                // model" can delete it and unloading leaves nothing behind.
                File.Move(sourcePath, destination, overwrite: true);
            }
        }
        catch (IOException)
        {
            return (ImportOutcome.Failed, null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (ImportOutcome.Failed, null, null);
        }

        var hash = ModelDownloader.Hash(destination);

        // A file the catalog knows is that entry, and counts as verified.
        var entry = catalog.Models.FirstOrDefault(model =>
            model.Sha256.Length > 0 && string.Equals(model.Sha256, hash, StringComparison.OrdinalIgnoreCase));

        var installed = new InstalledModel
        {
            Id = entry?.Id ?? CustomId(fileName),
            File = fileName,
            Sha256 = hash,
            Verified = entry is not null,
        };

        return (ImportOutcome.Imported, installed, entry);
    }

    /// <summary>A name for a model the catalog does not know: the file's own
    /// name, reduced to what an id may contain.</summary>
    private static string CustomId(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var id = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            id.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-');
        }

        return id.Length > 0 ? id.ToString() : "custom-model";
    }
}
