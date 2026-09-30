namespace Wordwright.Core.Snippets;

/// <summary>
/// The snippet validation rules from docs/ARCHITECTURE.md and the messages in
/// docs/UX_COPY.md (<c>Snippets.Error.ShortcutInvalid</c>,
/// <c>Snippets.Error.ShortcutTaken</c>, <c>Snippets.Warn.VeryLong</c>).
/// </summary>
public static class SnippetRules
{
    public const int MaxTriggerLength = 32;

    /// <summary>Above this, the editor warns but still saves (performance only).</summary>
    public const int LongBodyLength = 100_000;

    /// <summary>Letters, numbers, <c>-</c> or <c>_</c> only, 1–32 characters.</summary>
    public static bool IsValidTrigger(string? trigger)
    {
        if (string.IsNullOrEmpty(trigger) || trigger.Length > MaxTriggerLength)
        {
            return false;
        }

        foreach (var character in trigger)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Triggers are unique and case-sensitive: <c>sig</c> and <c>SIG</c>
    /// can both exist. Pass the snippet's own id as
    /// <paramref name="exceptId"/> when editing it.</summary>
    public static bool IsTriggerTaken(
        IEnumerable<Snippet> snippets,
        string trigger,
        string? exceptId = null)
    {
        return snippets.Any(snippet =>
            !string.Equals(snippet.Id, exceptId, StringComparison.Ordinal)
            && string.Equals(snippet.Trigger, trigger, StringComparison.Ordinal));
    }

    /// <summary>Snippets that should take part in expansion: enabled, with a
    /// trigger the matcher can safely look for.</summary>
    public static IEnumerable<Snippet> Expandable(IEnumerable<Snippet> snippets)
    {
        return snippets.Where(snippet => snippet.Enabled && IsValidTrigger(snippet.Trigger));
    }
}