using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _filePath;
    private readonly string _corruptPath;
    private readonly string _tempPath;

    /// <param name="directory">The directory that holds the settings file
    /// (in the app: <c>%AppData%\Wordwright</c>; in tests: a temp folder).</param>
    public SettingsStore(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
        _corruptPath = _filePath + ".corrupt";
        _tempPath = _filePath + ".tmp";
        DirectoryPath = directory;
    }

    public string DirectoryPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions)
                ?? new AppSettings();
        }
        catch (JsonException)
        {
            File.Move(_filePath, _corruptPath, overwrite: true);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);

        // Atomic on NTFS: write a sibling temp file, then replace in one move.
        File.WriteAllText(_tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(_tempPath, _filePath, overwrite: true);
    }
}