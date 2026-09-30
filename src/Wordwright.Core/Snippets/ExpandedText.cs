namespace Wordwright.Core.Snippets;

/// <summary>A snippet body with its variables resolved, ready to insert.</summary>
public sealed record ExpandedText
{
    public required string Text { get; init; }

    /// <summary>How many Left-arrow presses to send after pasting, so the caret
    /// ends where the body's <c>{cursor}</c> marker was. Zero when there was no
    /// marker, or when it was at the very end.</summary>
    public required int CharactersAfterCursor { get; init; }

    public static ExpandedText Empty => new() { Text = "", CharactersAfterCursor = 0 };
}