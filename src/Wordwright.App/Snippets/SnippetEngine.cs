using System.Diagnostics;
using System.Windows.Threading;
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
    /// (docs/ARCHITECTURE.md → Paste).</summary>
    private const int PasteSettleMilliseconds = 150;

    private readonly KeyboardHook _hook;
    private readonly ClipboardService _clipboard;
    private readonly Dispatcher _dispatcher;
    private readonly KeystrokeBuffer _buffer = new();
    private readonly VariableExpander _expander;

    private TriggerMatcher _matcher = new(SnippetDocument.DefaultTriggerPrefix, []);

    public SnippetEngine(KeyboardHook hook, ClipboardService clipboard, Dispatcher dispatcher)
    {
        _hook = hook;
        _clipboard = clipboard;
        _dispatcher = dispatcher;
        _expander = new VariableExpander(() => DateTimeOffset.Now, clipboard.GetText);

        _hook.CharacterTyped += OnCharacterTyped;
        _hook.BackspacePressed += OnBackspacePressed;
        _hook.BufferCleared += OnBufferCleared;
    }

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
        var expanded = _expander.Expand(match.Text);

        // Delete the typed trigger, put the text on the clipboard, paste, let the
        // target app read it, then put the user's clipboard back.
        InputSender.SendBackspaces(match.TypedLength);

        using (_clipboard.ReplaceWithText(expanded.Text + match.Delimiter))
        {
            InputSender.Paste();
            Thread.Sleep(PasteSettleMilliseconds);
        }

        if (expanded.CharactersAfterCursor > 0)
        {
            // Put the caret back where the body's {cursor} marker was.
            InputSender.SendLeftArrows(expanded.CharactersAfterCursor);
        }

        Debug.WriteLine($"snippet {match.Snippet.Trigger} expanded ({expanded.Text.Length} characters)");
    }

    private void OnBackspacePressed(object? sender, EventArgs e) => _buffer.Backspace();

    private void OnBufferCleared(object? sender, EventArgs e) => _buffer.Clear();

    public void Dispose()
    {
        _hook.CharacterTyped -= OnCharacterTyped;
        _hook.BackspacePressed -= OnBackspacePressed;
        _hook.BufferCleared -= OnBufferCleared;
    }
}