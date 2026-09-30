using System.Globalization;
using System.Text;

namespace Wordwright.Core.Snippets;

/// <summary>
/// Replaces the variables a snippet body may contain
/// (docs/ARCHITECTURE.md → Snippet engine, step 6):
/// <list type="bullet">
///   <item><c>{date}</c> — the system short date</item>
///   <item><c>{time}</c> — the system short time</item>
///   <item><c>{clipboard}</c> — the current clipboard text (nothing if empty)</item>
///   <item><c>{cursor}</c> — where the caret should end up; it produces no text,
///   and the caller sends Left arrows for the characters after it</item>
///   <item><c>{{</c> — a literal <c>{</c></item>
/// </list>
/// Anything else in braces stays exactly as typed, and the pass is not
/// recursive: text pulled in from the clipboard is never expanded again.
/// </summary>
public sealed class VariableExpander
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<string?> _readClipboard;

    /// <param name="clock">Supplies the current time; the app passes
    /// <c>DateTimeOffset.Now</c>, tests pass a fixed value.</param>
    /// <param name="readClipboard">Reads the clipboard text, or null when there
    /// is none. The app passes the clipboard service, tests pass a lambda.</param>
    public VariableExpander(Func<DateTimeOffset> clock, Func<string?> readClipboard)
    {
        _clock = clock;
        _readClipboard = readClipboard;
    }

    public ExpandedText Expand(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return ExpandedText.Empty;
        }

        var text = new StringBuilder(body.Length);
        int? cursorIndex = null;

        for (var index = 0; index < body.Length; index++)
        {
            if (body[index] != '{')
            {
                text.Append(body[index]);
                continue;
            }

            // "{{" is an escaped brace.
            if (index + 1 < body.Length && body[index + 1] == '{')
            {
                text.Append('{');
                index++;
                continue;
            }

            var closing = body.IndexOf('}', index + 1);
            if (closing < 0)
            {
                // No matching brace: keep the character as typed.
                text.Append(body[index]);
                continue;
            }

            var token = body[(index + 1)..closing];
            switch (token)
            {
                case "date":
                    text.Append(_clock().ToString("d", CultureInfo.CurrentCulture));
                    break;

                case "time":
                    text.Append(_clock().ToString("t", CultureInfo.CurrentCulture));
                    break;

                case "clipboard":
                    text.Append(_readClipboard() ?? "");
                    break;

                case "cursor":
                    // The first marker wins; any later one only disappears.
                    cursorIndex ??= text.Length;
                    break;

                default:
                    // Unknown variable: leave it as the user typed it.
                    text.Append('{').Append(token).Append('}');
                    break;
            }

            index = closing;
        }

        var expanded = text.ToString();

        return new ExpandedText
        {
            Text = expanded,
            CharactersAfterCursor = cursorIndex is { } cursor ? expanded.Length - cursor : 0,
        };
    }
}