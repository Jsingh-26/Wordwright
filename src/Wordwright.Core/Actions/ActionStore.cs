using System.IO;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Actions;

/// <summary>
/// Loads and saves <c>actions.json</c> (docs/ARCHITECTURE.md). Writes are atomic
/// and the previous version is kept as <c>actions.json.bak</c>; a corrupt file is
/// renamed <c>.corrupt</c> and the built-in actions are used, so a bad file is
/// preserved rather than overwritten.
/// </summary>
public sealed class ActionStore
{
    public const int SchemaVersion = 1;

    internal const string FileName = "actions.json";

    private readonly string _filePath;

    /// <param name="directory">The directory that holds the data files (in the
    /// app: <c>%AppData%\Wordwright</c>; in tests: a temp folder).</param>
    public ActionStore(string directory)
    {
        DirectoryPath = directory;
        _filePath = Path.Combine(directory, FileName);
    }

    public string DirectoryPath { get; }

    public ActionDocument Load() =>
        JsonFile.Read<ActionDocument>(_filePath) ?? ActionSeeds.Default();

    /// <summary>Loads the actions, writing the built-in set first when there is no
    /// file yet. A file that exists is never overwritten, so deleting a built-in
    /// keeps it deleted.</summary>
    public ActionDocument LoadOrSeed()
    {
        if (File.Exists(_filePath))
        {
            return Load();
        }

        var seeded = ActionSeeds.Default();
        Save(seeded);
        return seeded;
    }

    public void Save(ActionDocument document) =>
        JsonFile.Write(_filePath, document, keepBackup: true);

    /// <summary>Replaces one action, matching on id.</summary>
    public ActionDocument Update(ActionDocument document, AiAction action)
    {
        var updated = document with
        {
            Actions = document.Actions
                .Select(existing => existing.Id == action.Id ? action : existing)
                .ToList(),
        };

        Save(updated);
        return updated;
    }

    /// <summary>Resets a built-in action to the form in docs/UX_COPY.md, leaving
    /// the document alone when the id is not a built-in.</summary>
    public ActionDocument Reset(ActionDocument document, string id) =>
        ActionSeeds.BuiltIn(id) is { } builtIn ? Update(document, builtIn) : document;
}
