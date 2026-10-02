namespace Wordwright.Core.Snippets;

/// <summary>
/// The editor's autosave, without the window: one snippet open at a time, its
/// edits written once typing pauses for <see cref="SaveDelay"/>, and never
/// dropped (docs/PLAN.md P12.4, P13.3). Opening another snippet and
/// <see cref="Flush"/> write whatever is still waiting.
/// <para>
/// A shortcut is only written once the matcher could use it; until then the
/// stored one stays, while the name and text are saved whatever the shortcut
/// field holds (docs/UX_COPY.md → Snippets).
/// </para>
/// <para>
/// The session keeps no timer of its own. The caller runs one and calls
/// <see cref="SaveIfDue"/> when it fires; <see cref="DueUtc"/> says when that is.
/// </para>
/// </summary>
public sealed class SnippetEditSession
{
    /// <summary>How long typing pauses before the edits are written.</summary>
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<SnippetDocument> _document;
    private readonly Action<SnippetDocument> _write;

    private string _name = string.Empty;
    private string _shortcut = string.Empty;
    private string _body = string.Empty;

    /// <param name="clock">The current time (UTC is fine), for the pause and
    /// for <see cref="Snippet.UpdatedUtc"/>.</param>
    /// <param name="document">The document as it is now.</param>
    /// <param name="write">Stores a changed document.</param>
    public SnippetEditSession(
        Func<DateTimeOffset> clock,
        Func<SnippetDocument> document,
        Action<SnippetDocument> write)
    {
        _clock = clock;
        _document = document;
        _write = write;
    }

    /// <summary>The snippet in the editor, as last stored; null when none is open.</summary>
    public Snippet? Snippet { get; private set; }

    /// <summary>When the waiting edits should be written; null when nothing waits.</summary>
    public DateTimeOffset? DueUtc { get; private set; }

    /// <summary>True when there are edits not yet written.</summary>
    public bool IsPending => DueUtc is not null;

    /// <summary>Puts <paramref name="snippet"/> in the editor, first writing any
    /// edits still waiting for the one before it.</summary>
    /// <returns>The outgoing snippet as written, or null when nothing waited.</returns>
    public Snippet? Open(Snippet? snippet)
    {
        var saved = Flush();

        Snippet = snippet;
        _name = snippet?.Name ?? string.Empty;
        _shortcut = snippet?.Trigger ?? string.Empty;
        _body = snippet?.Body ?? string.Empty;
        return saved;
    }

    /// <summary>Records what the editor fields hold now; the write waits for
    /// the typing to pause.</summary>
    public void Edit(string name, string shortcut, string body)
    {
        if (Snippet is null)
        {
            return;
        }

        _name = name;
        _shortcut = shortcut;
        _body = body;
        DueUtc = _clock() + SaveDelay;
    }

    /// <summary>Writes the waiting edits if the pause is over.</summary>
    /// <returns>The snippet as written, or null when nothing was due.</returns>
    public Snippet? SaveIfDue()
    {
        return DueUtc is { } due && _clock() >= due ? Save() : null;
    }

    /// <summary>Writes the waiting edits now, if there are any.</summary>
    /// <returns>The snippet as written, or null when nothing waited.</returns>
    public Snippet? Flush()
    {
        return IsPending ? Save() : null;
    }

    /// <summary>Forgets the waiting edits, e.g. when the snippet is being deleted.</summary>
    public void Discard()
    {
        DueUtc = null;
    }

    /// <summary>True when <paramref name="shortcut"/> can be written for the
    /// open snippet: empty (a work in progress), or valid and not taken.</summary>
    public bool IsUsable(string shortcut)
    {
        return shortcut.Length == 0
            || (SnippetRules.IsValidTrigger(shortcut)
                && !SnippetRules.IsTriggerTaken(_document().Snippets, shortcut, Snippet?.Id));
    }

    private Snippet? Save()
    {
        DueUtc = null;

        if (Snippet is not { } current)
        {
            return null;
        }

        var document = _document();
        var prefix = document.TriggerPrefix;

        // The trigger is stored without the prefix, so a stray prefix typed into
        // the shortcut field never reaches the file. A shortcut the matcher could
        // not use keeps the stored one.
        var trigger = !IsUsable(_shortcut)
            ? current.Trigger
            : _shortcut.StartsWith(prefix, StringComparison.Ordinal)
                ? _shortcut[prefix.Length..]
                : _shortcut;

        var updated = current with
        {
            Trigger = trigger,
            Name = _name,
            Body = _body,
            UpdatedUtc = _clock(),
        };

        _write(document with
        {
            Snippets = document.Snippets
                .Select(snippet => snippet.Id == updated.Id ? updated : snippet)
                .ToList(),
        });

        Snippet = updated;
        return updated;
    }
}
