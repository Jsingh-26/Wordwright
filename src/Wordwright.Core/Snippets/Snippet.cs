namespace Wordwright.Core.Snippets;

/// <summary>
/// One snippet, as stored in <c>snippets.json</c> (docs/ARCHITECTURE.md).
/// <see cref="Trigger"/> is stored without the prefix: the user types
/// <c>prefix + trigger</c>. Fields are optional on read so a hand-edited file
/// still loads; <see cref="SnippetRules"/> is what decides whether a snippet
/// is usable.
/// </summary>
public sealed record Snippet
{
    public string Id { get; init; } = "";

    public string Trigger { get; init; } = "";

    public string Name { get; init; } = "";

    public string Body { get; init; } = "";

    public bool Enabled { get; init; } = true;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>A new short identifier (8 hex characters), used to tell snippets
    /// apart when names or triggers are edited.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}