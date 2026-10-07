using System.IO;
using Wordwright.Core.Settings;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Storage;

/// <summary>
/// Brings the installer build's data into the Store build's own folder on its
/// first run (docs/PLAN.md P14.1). The Store build cannot share
/// <c>%AppData%\Wordwright</c> (Microsoft refused the capability that allows it),
/// so a user who switches builds would otherwise start again from the examples.
/// </summary>
public static class DataFolderImport
{
    private static readonly string[] FileNames = [SettingsStore.FileName, SnippetStore.FileName];

    /// <summary>
    /// Copies <c>settings.json</c> and <c>snippets.json</c> from
    /// <paramref name="source"/> into <paramref name="destination"/>, only when the
    /// destination has neither yet. Nothing is moved or deleted, and an existing
    /// file is never overwritten. Returns true when anything was copied.
    /// </summary>
    public static bool CopyIfEmpty(string source, string destination)
    {
        if (FileNames.Any(name => File.Exists(Path.Combine(destination, name))))
        {
            return false;
        }

        var copied = false;
        foreach (var name in FileNames)
        {
            var from = Path.Combine(source, name);
            if (!File.Exists(from))
            {
                continue;
            }

            Directory.CreateDirectory(destination);
            File.Copy(from, Path.Combine(destination, name), overwrite: false);
            copied = true;
        }

        return copied;
    }
}
