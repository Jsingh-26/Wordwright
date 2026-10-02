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

    /// <summary>
    /// The same text with every line break as <c>\r\n</c> (docs/PLAN.md P12.8).
    /// Snippets from the seeds or an import use <c>\n</c>, which classic Win32
    /// edit controls and several older apps show as nothing at all. The caret
    /// count is redone to match: an edit control steps over <c>\r\n</c> with one
    /// Left arrow, so each line break after the cursor counts once.
    /// </summary>
    public ExpandedText WithWindowsLineEndings()
    {
        var cursor = Text.Length - CharactersAfterCursor;
        var before = ToCrLf(Text[..cursor]);
        var after = ToCrLf(Text[cursor..]);

        return new ExpandedText
        {
            Text = before + after,
            CharactersAfterCursor = after.Length - CountCrLf(after),
        };
    }

    private static string ToCrLf(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private static int CountCrLf(string text)
    {
        var count = 0;
        for (var index = text.IndexOf("\r\n", StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf("\r\n", index + 2, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}