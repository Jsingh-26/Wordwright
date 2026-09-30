namespace Wordwright.Core.Snippets;

/// <summary>What to do when the keystroke buffer ends with a snippet's trigger.</summary>
public sealed record TriggerMatch
{
    /// <summary>The snippet that was triggered.</summary>
    public required Snippet Snippet { get; init; }

    /// <summary>How many typed characters to delete before inserting
    /// (the prefix and trigger, plus the delimiter in the deferred case).</summary>
    public required int TypedLength { get; init; }

    /// <summary>The character the user typed straight after an ambiguous trigger,
    /// kept after the expansion; empty when the trigger expanded on its own.</summary>
    public required string Delimiter { get; init; }

    /// <summary>The snippet body as stored; variables are expanded later
    /// (see <c>VariableExpander</c>).</summary>
    public string Text => Snippet.Body;
}