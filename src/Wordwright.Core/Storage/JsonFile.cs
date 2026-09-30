using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wordwright.Core.Storage;

/// <summary>
/// The JSON file plumbing the data files share: one set of serializer options,
/// a tolerant read that never throws away a corrupt file, and an atomic write.
/// </summary>
internal static class JsonFile
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Reads and deserialises a file. A missing file yields <c>null</c>. A corrupt
    /// file is renamed <c>.corrupt</c> first — nothing is deleted — and yields
    /// <c>null</c>, so the caller can fall back to defaults.
    /// </summary>
    internal static T? Read<T>(string path)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            File.Move(path, path + ".corrupt", overwrite: true);
            return null;
        }
    }

    /// <summary>
    /// Writes atomically: a sibling <c>.tmp</c> file, then a single replace. With
    /// <paramref name="keepBackup"/>, the replaced version stays as <c>.bak</c>.
    /// </summary>
    internal static void Write<T>(string path, T value, bool keepBackup)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, Options));

        if (keepBackup && File.Exists(path))
        {
            File.Replace(temporaryPath, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, path, overwrite: true);
        }
    }
}