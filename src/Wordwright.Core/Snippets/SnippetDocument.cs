namespace Wordwright.Core.Snippets;

/// <summary>The whole <c>snippets.json</c> document.</summary>
public sealed record SnippetDocument
{
    public const string DefaultTriggerPrefix = ";";

    public int SchemaVersion { get; init; } = SnippetStore.SchemaVersion;

    /// <summary>The character(s) typed before a trigger. One user-wide setting,
    /// so the Snippets page edits it here.</summary>
    public string TriggerPrefix { get; init; } = DefaultTriggerPrefix;

    public IReadOnlyList<Snippet> Snippets { get; init; } = [];
}