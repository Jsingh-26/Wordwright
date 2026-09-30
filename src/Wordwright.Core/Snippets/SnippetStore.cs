using System.IO;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Snippets;

/// <summary>
/// Loads and saves <c>snippets.json</c>. Writes are atomic and the previous
/// version is kept as <c>snippets.json.bak</c> (docs/ARCHITECTURE.md). A corrupt
/// file is renamed <c>snippets.json.corrupt</c> and the defaults are used, so a
/// bad file is preserved rather than overwritten.
/// </summary>
public sealed class SnippetStore
{
    public const int SchemaVersion = 1;

    internal const string FileName = "snippets.json";

    private readonly string _filePath;

    /// <param name="directory">The directory that holds the data files (in the
    /// app: <c>%AppData%\Wordwright</c>; in tests: a temp folder).</param>
    public SnippetStore(string directory)
    {
        DirectoryPath = directory;
        _filePath = Path.Combine(directory, FileName);
    }

    public string DirectoryPath { get; }

    public SnippetDocument Load()
    {
        return JsonFile.Read<SnippetDocument>(_filePath) ?? new SnippetDocument();
    }

    /// <summary>Loads the snippets, writing the example set first when there is no
    /// file yet — the first-run seeding of docs/PLAN.md P2.6. A file that exists is
    /// never overwritten, so deleting the examples keeps them deleted.</summary>
    public SnippetDocument LoadOrSeed()
    {
        if (File.Exists(_filePath))
        {
            return Load();
        }

        var seeded = SnippetSeeds.Default();
        Save(seeded);
        return seeded;
    }

    public void Save(SnippetDocument document)
    {
        JsonFile.Write(_filePath, document, keepBackup: true);
    }
}