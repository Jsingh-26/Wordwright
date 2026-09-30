namespace Wordwright.Core.Snippets;

/// <summary>
/// The snippet variables (docs/ARCHITECTURE.md → Snippet engine, step 6). The
/// editor's Insert buttons spell the braced form (<see cref="Date"/>), and
/// <see cref="VariableExpander"/> matches the name inside the braces
/// (<see cref="DateName"/>); both come from here, so the two cannot drift apart.
/// </summary>
public static class SnippetVariables
{
    public const string DateName = "date";

    public const string TimeName = "time";

    public const string ClipboardName = "clipboard";

    public const string CursorName = "cursor";

    /// <summary>Today's short date, as typed in a body.</summary>
    public const string Date = "{" + DateName + "}";

    /// <summary>The current short time, as typed in a body.</summary>
    public const string Time = "{" + TimeName + "}";

    /// <summary>The clipboard's text, as typed in a body.</summary>
    public const string Clipboard = "{" + ClipboardName + "}";

    /// <summary>Where the caret should end up, as typed in a body.</summary>
    public const string Cursor = "{" + CursorName + "}";
}