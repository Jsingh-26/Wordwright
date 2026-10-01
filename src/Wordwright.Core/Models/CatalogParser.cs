using System.Reflection;
using System.Text.Json;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Models;

/// <summary>
/// Reads <c>models.json</c> (docs/MODELS.md → Catalog file). The app ships a
/// copy embedded at build time and only replaces it with a downloaded one that
/// this parser accepts, so nothing a new catalog says can break a running app.
/// </summary>
public static class CatalogParser
{
    /// <summary>The newest schema this build understands. A catalog that
    /// declares a newer one is refused whole rather than half-read.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>The embedded copy's resource name.</summary>
    private const string EmbeddedResourceName = "Wordwright.Core.models.json";

    /// <summary>
    /// The catalog in a file's text, or null when it cannot be used: not JSON,
    /// not a catalog, or written to a newer schema.
    /// </summary>
    public static ModelCatalog? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        ModelCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<ModelCatalog>(json, JsonFile.Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (catalog is null ||
            catalog.SchemaVersion > SupportedSchemaVersion ||
            string.IsNullOrWhiteSpace(catalog.CatalogVersion))
        {
            return null;
        }

        // An entry without an id or a tier cannot be recommended or downloaded.
        return catalog with
        {
            Models = [.. catalog.Models.Where(model => model.Id.Length > 0 && model.Tiers.Count > 0)],
        };
    }

    /// <summary>The copy embedded in this build. Never null: a build without it
    /// is a mistake the tests would have caught.</summary>
    public static ModelCatalog Embedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"{EmbeddedResourceName} is not embedded in this build.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd())
            ?? throw new InvalidOperationException($"{EmbeddedResourceName} is not a usable model catalog.");
    }
}
