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
        var settings = JsonFile.Read<AppSettings>(_filePath) ?? new AppSettings();

        // A missing or unknown theme name falls back to System (docs/PLAN.md P11.2),
        // so a hand-edited or older settings.json can never leave the app themeless.
        return settings with { Theme = NormalizeTheme(settings.Theme) };
    }

    public void Save(AppSettings settings)
    {
        JsonFile.Write(_filePath, settings, keepBackup: false);
    }

    /// <summary>Returns one of "system", "light" or "dark"; anything else is System.</summary>
    internal static string NormalizeTheme(string? theme) => theme?.ToLowerInvariant() switch
    {
        "light" => "light",
        "dark" => "dark",
        _ => "system",
    };
}