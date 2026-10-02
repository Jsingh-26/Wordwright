using System.Diagnostics;
using System.Windows.Threading;
using EventLog = Wordwright.Core.Diagnostics.EventLog;
using Wordwright.Core.Keystrokes;
using Wordwright.Core.Snippets;
using Wordwright.Platform.Input;
using Wordwright.Platform.Keyboard;

namespace Wordwright.App.Snippets;

/// <summary>
/// Joins the pieces of the snippet engine: the keyboard hook reports what was
/// typed, <see cref="TriggerMatcher"/> asks whether it ended a trigger, and a
/// match replaces the typed trigger with the snippet's text
/// (docs/ARCHITECTURE.md → Snippet engine, steps 4–6).
/// </summary>
internal sealed class SnippetEngine : IDisposable
{
    /// <summary>How long our text stays on the clipboard after Ctrl+V, so the
    /// target app has read it before the old contents go back
    /// (docs/ARCHITECTURE.md → Paste). Slow targets (Electron apps under load,
    /// Remote Desktop) read late, so this is generous; nothing waits on it, and
    /// another expansion inside the window reuses the saved clipboard.</summary>
    private static readonly TimeSpan RestoreDelay = TimeSpan.FromMilliseconds(400);

    private readonly KeyboardHook _hook;
    private readonly ClipboardService _clipboard;
    private readonly Dispatcher _dispatcher;
    private readonly EventLog? _log;
    private readonly KeystrokeBuffer _buffer = new();
    private readonly VariableExpander _expander;
    private readonly DispatcherTimer _restoreTimer;

    private TriggerMatcher _matcher = new(SnippetDocument.DefaultTriggerPrefix, []);

    public SnippetEngine(KeyboardHook hook, ClipboardService clipboard, Dispatcher dispatcher, EventLog? log)
    {
        _log = log;
        _hook = hook;
        _clipboard = clipboard;
        _dispatcher = dispatcher;
        _expander = new VariableExpander(() => DateTimeOffset.Now, clipboard.GetText);

        _restoreTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = RestoreDelay };
        _restoreTimer.Tick += (_, _) =>
        {
            _restoreTimer.Stop();
            _clipboard.RestoreSaved();
        };

        _hook.CharacterTyped += OnCharacterTyped;
        _hook.BackspacePressed += OnBackspacePressed;
        _hook.BufferCleared += OnBufferCleared;
    }

    /// <summary>A snippet was just expanded. Raised on the UI thread, which is
    /// where the welcome's playground reacts to its first one.</summary>
    public event EventHandler? Expanded;

    /// <summary>Takes the snippets the matcher should look for; call it again
    /// whenever the user edits them.</summary>
    public void Apply(SnippetDocument document)
    {
        _matcher = new TriggerMatcher(document.TriggerPrefix, document.Snippets);
    }

    private void OnCharacterTyped(object? sender, char character)
    {
        _buffer.Append(character);

        if (_matcher.TryMatch(_buffer.Text) is not { } match)
        {
            return;
        }

        _buffer.RemoveLast(match.TypedLength);

        try
        {
            // The clipboard is an OLE object, so the paste belongs on the UI
            // thread. Invoking rather than posting keeps a second expansion from
            // starting while this one is still pasting.
            _dispatcher.Invoke(() => Expand(match));
        }
        catch (TaskCanceledException)
        {
            // The app is shutting down; there is nothing left to expand into.
        }
    }

    private void Expand(TriggerMatch match)
    {
        // Every line break goes out as \r\n, which every Windows app shows as a
        // new line (docs/PLAN.md P12.8).
        var expanded = _expander.Expand(match.Text).WithWindowsLineEndings();

        // Put the text on the clipboard, delete the typed trigger and paste. The
        // user's clipboard goes back once the target app has had time to read it.
        _restoreTimer.Stop();
        if (!_clipboard.PutTextForPaste(expanded.Text + match.Delimiter))
        {
            // Another program is holding the clipboard. Pasting now would insert
            // whatever it holds, so the typed shortcut stays as it is
            // (docs/PLAN.md P13.1); a restore still waiting from an earlier
            // expansion goes ahead as planned.
            _restoreTimer.Start();
            _log?.Write("clipboard busy");
            return;
        }

        InputSender.SendBackspaces(match.TypedLength);
        InputSender.Paste();

        if (expanded.CharactersAfterCursor > 0)
        {
            // Put the caret back where the body's {cursor} marker was. The arrows
            // queue behind Ctrl+V, so the target handles them after the paste.
            InputSender.SendLeftArrows(expanded.CharactersAfterCursor);
        }

        _restoreTimer.Start();

        Debug.WriteLine($"snippet {match.Snippet.Trigger} expanded ({expanded.Text.Length} characters)");

        Expanded?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackspacePressed(object? sender, EventArgs e) => _buffer.Backspace();

    private void OnBufferCleared(object? sender, EventArgs e) => _buffer.Clear();

    public void Dispose()
    {
        // Quitting (or rebuilding the engine) must not leave our text behind.
        _restoreTimer.Stop();
        _clipboard.RestoreSaved();

        _hook.CharacterTyped -= OnCharacterTyped;
        _hook.BackspacePressed -= OnBackspacePressed;
        _hook.BufferCleared -= OnBufferCleared;
    }
}