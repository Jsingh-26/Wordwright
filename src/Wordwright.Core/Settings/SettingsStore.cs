using System.IO;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Settings;

/// <summary>
/// Loads and saves <c>settings.json</c> under the app's user-data directory.
/// Writes are atomic (temp file, then replace). A corrupt file is renamed
/// <c>settings.json.corrupt</c> and the defaults are used, so the user never
/// ends up with no settings because of a truncated write.
/// </summary>
public sealed class SettingsStore
{
    public const int SchemaVersion = 1;

    internal const string FileName = "settings.json";

    private readonly string _filePath;

    /// <param name="directory">The directory that holds the settings file
    /// (in the app: <c>%AppData%\Wordwright</c>; in tests: a temp folder).</param>
    public SettingsStore(string directory)
    {
        DirectoryPath = directory;
        _filePath = Path.Combine(directory, FileName);
    }

    public string DirectoryPath { get; }

    public AppSettings Load()
    {
        return JsonFile.Read<AppSettings>(_filePath) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        JsonFile.Write(_filePath, settings, keepBackup: false);
    }
}