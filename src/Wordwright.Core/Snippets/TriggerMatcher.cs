namespace Wordwright.Core.Snippets;

/// <summary>
/// Looks for a snippet trigger at the end of the keystroke buffer
/// (docs/ARCHITECTURE.md → Snippet engine, step 4).
/// <para>
/// A match needs the buffer to end with <c>prefix + trigger</c>, with the
/// character before the prefix at the start of the buffer, whitespace or
/// punctuation — so <c>a;sig</c> does not expand. Triggers are case-sensitive.
/// </para>
/// <para>
/// When one trigger begins another (<c>s</c> and <c>sig</c>), the shorter one is
/// ambiguous while the user is still typing, so it waits: it expands only once a
/// space or punctuation arrives, and that character stays after the expansion.
/// </para>
/// </summary>
public sealed class TriggerMatcher
{
    private readonly string _prefix;
    private readonly IReadOnlyList<Snippet> _snippets;

    /// <param name="prefix">The snippet prefix from the user's settings, e.g. ";".</param>
    /// <param name="snippets">All snippets; disabled and invalid ones are ignored.</param>
    public TriggerMatcher(string prefix, IEnumerable<Snippet> snippets)
    {
        _prefix = prefix ?? "";

        // Longest trigger first: "sig" must win over "s" in the same buffer.
        _snippets = SnippetRules
            .Expandable(snippets)
            .OrderByDescending(snippet => snippet.Trigger.Length)
            .ThenBy(snippet => snippet.Trigger, StringComparer.Ordinal)
            .ToList();
    }

    /// <param name="buffer">The rolling buffer of typed characters, ending at the
    /// caret. Null or empty never matches.</param>
    public TriggerMatch? TryMatch(string? buffer)
    {
        if (string.IsNullOrEmpty(buffer))
        {
            return null;
        }

        foreach (var snippet in _snippets)
        {
            var needle = _prefix + snippet.Trigger;

            // Complete trigger at the end of the buffer: expand straight away,
            // unless a longer trigger could still be on its way.
            if (buffer.EndsWith(needle, StringComparison.Ordinal)
                && HasBoundaryBefore(buffer, buffer.Length - needle.Length)
                && !IsAmbiguous(snippet.Trigger))
            {
                return new TriggerMatch
                {
                    Snippet = snippet,
                    TypedLength = needle.Length,
                    Delimiter = "",
                };
            }

            // Trigger followed by a space or punctuation: expand and keep that
            // character after the inserted text. This is what an ambiguous trigger
            // waits for, and it also copes with the delimiter arriving in the same
            // batch as the last trigger character.
            if (buffer.Length > needle.Length
                && IsDelimiter(buffer[^1])
                && buffer.AsSpan(0, buffer.Length - 1).EndsWith(needle, StringComparison.Ordinal)
                && HasBoundaryBefore(buffer, buffer.Length - needle.Length - 1))
            {
                return new TriggerMatch
                {
                    Snippet = snippet,
                    TypedLength = needle.Length + 1,
                    Delimiter = buffer[^1].ToString(),
                };
            }
        }

        return null;
    }

    /// <summary>True when another trigger starts with this one, so this trigger
    /// cannot expand until the user shows they have finished typing it.</summary>
    private bool IsAmbiguous(string trigger)
    {
        return _snippets.Any(other =>
            other.Trigger.Length > trigger.Length
            && other.Trigger.StartsWith(trigger, StringComparison.Ordinal));
    }

    /// <summary>Start of the buffer, whitespace or punctuation. A preceding letter,
    /// digit or underscore means the trigger is inside a word.</summary>
    private static bool HasBoundaryBefore(string buffer, int index)
    {
        return index <= 0 || !IsWordCharacter(buffer[index - 1]);
    }

    private static bool IsWordCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character == '_';
    }

    private static bool IsDelimiter(char character)
    {
        return char.IsWhiteSpace(character) || char.IsPunctuation(character);
    }
}