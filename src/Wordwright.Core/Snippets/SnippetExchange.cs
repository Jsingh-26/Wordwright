using System.IO;
using System.Text.Json;

namespace Wordwright.Core.Snippets;

/// <summary>The outcome of importing a snippets file.</summary>
/// <param name="Document">The library with the imported snippets added.</param>
/// <param name="Added">How many snippets were taken in.</param>
/// <param name="Skipped">How many were left out because their shortcut was
/// already in use, or was not one the matcher could use.</param>
public sealed record SnippetMerge(SnippetDocument Document, int Added, int Skipped);

/// <summary>
/// Export and import of snippets as a JSON file (docs/PLAN.md P3.3). An import
/// adds to the library — the user's own snippets are never replaced — and each
/// imported snippet gets a fresh id so two libraries cannot collide.
/// </summary>
public static class SnippetExchange
{
    /// <summary>Writes the library to a file the user picked.</summary>
    public static void Export(string path, SnippetDocument document)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(document, ExportOptions));
    }

    /// <summary>Reads a file the user picked; null when it is not a snippets
    /// export. The file is left exactly as it is, whatever it contains.</summary>
    public static SnippetDocument? Import(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<SnippetDocument>(File.ReadAllText(path), ExportOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adds <paramref name="imported"/> to <paramref name="target"/>. A snippet
    /// whose shortcut is already used — by the library or by another snippet in
    /// the file — is skipped, so the rules in docs/ARCHITECTURE.md still hold
    /// afterwards.
    /// </summary>
    public static SnippetMerge Merge(SnippetDocument target, SnippetDocument imported)
    {
        var added = new List<Snippet>(target.Snippets);
        var addedCount = 0;
        var skippedCount = 0;

        foreach (var snippet in imported.Snippets)
        {
            if (!SnippetRules.IsValidTrigger(snippet.Trigger)
                || SnippetRules.IsTriggerTaken(added, snippet.Trigger))
            {
                skippedCount++;
                continue;
            }

            added.Add(snippet with { Id = Snippet.NewId() });
            addedCount++;
        }

        return new SnippetMerge(target with { Snippets = added }, addedCount, skippedCount);
    }

    /// <summary>The exported file uses the same shape as snippets.json, indented
    /// and readable, since the user may open it themselves.</summary>
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}