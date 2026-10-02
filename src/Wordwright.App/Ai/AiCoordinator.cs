using System.Windows;
using Wordwright.App.Palette;
using Wordwright.App.Pill;
using Wordwright.App.Resources;
using Wordwright.Core.Actions;
using Wordwright.Core.Snippets;
using Wordwright.Platform.Hotkeys;
using Wordwright.Platform.Input;
using Wordwright.Platform.Keyboard;

namespace Wordwright.App.Ai;

/// <summary>
/// Connects the hotkeys to the AI surface and runs the rewrite flow
/// (docs/ARCHITECTURE.md → AI rewrite flow, docs/PLAN.md P6.5–P6.7):
/// the palette hotkey opens the palette, an action's own hotkey runs it directly,
/// the selection is captured, the model rewrites it, a progress pill shows how it
/// is going, Esc cancels, and the result is pasted (or copied when the target app
/// is elevated). It re-registers hotkeys whenever settings or actions change.
/// </summary>
internal sealed class AiCoordinator : IDisposable
{
    /// <summary>The registration id of the palette hotkey.</summary>
    internal const string PaletteHotkeyId = "palette";

    private const string ActionIdPrefix = "action:";

    /// <summary>A rewrite with no calibration yet still needs a ruler tick to
    /// grow against; this is the placeholder until P7.1 measures the real time.</summary>
    private static readonly TimeSpan DefaultEstimate = TimeSpan.FromSeconds(8);

    /// <summary>How long our snippet text stays on the clipboard after Ctrl+V.</summary>
    private const int PasteSettleMilliseconds = 150;

    private readonly App _app;
    private readonly HotkeyService _hotkeys = new();
    private readonly ClipboardService _clipboard = new();

    /// <summary>Expands a palette-inserted snippet's variables, reading the clock
    /// and the clipboard at insert time (before the clipboard is replaced).</summary>
    private readonly VariableExpander _expander;

    private PaletteWindow? _palette;
    private PillWindow? _pill;
    private EscapeWatcher? _escape;
    private CancellationTokenSource? _rewrite;
    private bool _running;
    private IntPtr _previousForeground;

    public AiCoordinator(App app)
    {
        _app = app;
        _expander = new VariableExpander(() => DateTimeOffset.Now, _clipboard.GetText);
        _hotkeys.Pressed += OnPressed;
        Apply();
    }

    public HotkeyService Hotkeys => _hotkeys;

    /// <summary>Re-registers the palette hotkey and every enabled action's own
    /// hotkey (docs/ARCHITECTURE.md → Hotkeys).</summary>
    public void Apply()
    {
        var registrations = new List<HotkeyRegistration>();

        if (HotkeySpec.Parse(_app.Settings.PaletteHotkey) is { } palette)
        {
            registrations.Add(new HotkeyRegistration(PaletteHotkeyId, palette));
        }

        foreach (var action in _app.Actions.Actions.Where(action => action.Enabled))
        {
            if (HotkeySpec.Parse(action.Hotkey) is { } spec)
            {
                registrations.Add(new HotkeyRegistration(ActionId(action.Id), spec));
            }
        }

        _hotkeys.Apply(registrations);
    }

    /// <summary>The registration id for an action's own hotkey.</summary>
    internal static string ActionId(string actionId) => ActionIdPrefix + actionId;

    private void OnPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (e.Id == PaletteHotkeyId)
        {
            OpenPalette();
            return;
        }

        if (e.Id.StartsWith(ActionIdPrefix, StringComparison.Ordinal))
        {
            var actionId = e.Id[ActionIdPrefix.Length..];
            if (_app.Actions.Actions.FirstOrDefault(action => action.Id == actionId) is { } action)
            {
                _ = RunDirectAsync(action);
            }
        }
    }

    /// <summary>Opens the palette near the caret, or brings the open one forward.</summary>
    internal void OpenPalette()
    {
        if (_palette is { IsVisible: true })
        {
            _palette.Activate();
            return;
        }

        if (_running)
        {
            // One rewrite at a time; the pill is already up.
            return;
        }

        var snippets = SnippetRules.Expandable(_app.Snippets.Snippets).ToList();
        var prefix = _app.Snippets.TriggerPrefix;
        var aiOn = _app.Settings.AiEnabled && _app.Rewrite.ActiveModel() is not null;

        // Capture the selection first: with AI off and nothing selected, the
        // palette opens straight to the snippet list (P6.8).
        var selection = CaptureSelection();

        var mode = (aiOn, selection is not null) switch
        {
            (true, true) => PaletteMode.Actions,
            (false, true) => PaletteMode.AiOff,
            _ => PaletteMode.SnippetsOnly,
        };

        _previousForeground = ForegroundWindow.Current();

        var palette = new PaletteWindow(_app.Actions.Actions, snippets, prefix, mode)
        {
            Selection = selection,
        };

        palette.TurnOnAiRequested += (_, _) => new ConsentWindow().Show();
        palette.Chosen += (_, _) => OnPaletteChosen(palette);
        _palette = palette;
        palette.Closed += (_, _) => _palette = null;
        palette.Show();
    }

    private void OnPaletteChosen(PaletteWindow palette)
    {
        if (palette.ChosenSnippet is { } snippet)
        {
            InsertSnippet(snippet);
            return;
        }

        if (palette.ChosenAction is not { } action || palette.Selection is not { } selection)
        {
            return;
        }

        // Give the app the user was writing in its focus back before we paste.
        ForegroundWindow.Restore(_previousForeground);
        _ = RunAsync(action, selection, palette.CustomInstruction);
    }

    /// <summary>
    /// Inserts a snippet the user picked in the palette at the caret, through the
    /// normal snippet path (variables included) and with the clipboard restored
    /// (docs/PLAN.md P6.8).
    /// </summary>
    private void InsertSnippet(Snippet snippet)
    {
        ForegroundWindow.Restore(_previousForeground);

        var expanded = _expander.Expand(snippet.Body);

        using (_clipboard.ReplaceWithText(expanded.Text))
        {
            InputSender.Paste();
            Thread.Sleep(PasteSettleMilliseconds);
        }

        if (expanded.CharactersAfterCursor > 0)
        {
            InputSender.SendLeftArrows(expanded.CharactersAfterCursor);
        }
    }

    /// <summary>Runs an action from its own hotkey, capturing the selection now.</summary>
    private async Task RunDirectAsync(AiAction action)
    {
        if (_running)
        {
            return;
        }

        if (!_app.Settings.AiEnabled || _app.Rewrite.ActiveModel() is null)
        {
            ShowPill(Strings.Get("Pill.AiOff"), PillState.Error);
            return;
        }

        if (CaptureSelection() is not { } selection)
        {
            ShowPill(Strings.Get("Pill.NoSelection"), PillState.Error);
            return;
        }

        await RunAsync(action, selection, customInstruction: null);
    }

    /// <summary>The rewrite itself: capture already done, run, then deliver.</summary>
    private async Task RunAsync(AiAction action, string selection, string? customInstruction)
    {
        var instruction = action.Id == "custom"
            ? customInstruction ?? ""
            : action.Instruction;

        if (instruction.Trim().Length == 0)
        {
            return;
        }

        _running = true;
        _rewrite = new CancellationTokenSource();

        var pill = ShowPillWindow();
        var dispatcher = _app.Dispatcher;

        // The model runs on pool threads, so every pill change hops back here.
        void Post(Action action) => dispatcher.BeginInvoke(action);

        _escape = new EscapeWatcher();
        _escape.Pressed += (_, _) => Post(pill.ReportEscape);
        pill.CancelRequested += (_, _) => _rewrite?.Cancel();
        _escape.Start();

        try
        {
            var outcome = await _app.Rewrite.RewriteAsync(
                instruction,
                selection,
                generationStarted: () => Post(pill.ShowWorking),
                cancellationToken: _rewrite.Token);

            switch (outcome.Status)
            {
                case RewriteStatus.Done:
                    Deliver(pill, outcome.Text);
                    break;

                case RewriteStatus.TooLong:
                    Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.TooLong")));
                    break;

                case RewriteStatus.BadOutput:
                    Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.BadOutput")));
                    break;

                case RewriteStatus.NotEnoughResources:
                    Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.NotEnoughResources")));
                    break;

                default:
                    Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.AiOff")));
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.Cancelled")));
        }
        catch (Exception exception)
        {
            // Never let a rewrite take the tray app down.
            System.Diagnostics.Debug.WriteLine($"rewrite failed: {exception.GetType().Name}");
            Post(() => pill.ShowResult(PillState.Error, Strings.Get("Pill.BadOutput")));
        }
        finally
        {
            Post(() =>
            {
                _escape?.Dispose();
                _escape = null;
                _rewrite?.Dispose();
                _rewrite = null;
                _running = false;
            });
        }
    }

    private void Deliver(PillWindow pill, string text)
    {
        // Pasting touches the clipboard, which is an OLE object: it belongs on
        // the UI thread.
        var delivery = PasteHelper.Deliver(_clipboard, text);

        if (delivery == RewriteDelivery.Copied)
        {
            pill.ShowResult(PillState.Done, Strings.Get("Pill.AdminApp.Copied"), sixSeconds: true);
        }
        else
        {
            pill.ShowResult(PillState.Done, Strings.Get("Pill.Done"));
        }
    }

    private string? CaptureSelection()
    {
        try
        {
            return SelectionCapture.Capture(_clipboard);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"selection capture failed: {exception.GetType().Name}");
            return null;
        }
    }

    private PillWindow ShowPillWindow()
    {
        var caret = CaretPosition.Current();
        var pill = new PillWindow { Estimate = DefaultEstimate };

        pill.Show();
        pill.Start(caret.X, caret.Y);

        if (_app.Rewrite.IsLoaded)
        {
            pill.ShowWorking();
        }
        else
        {
            pill.ShowLoading();
        }

        _pill = pill;
        pill.Closed += (_, _) => _pill = null;
        return pill;
    }

    /// <summary>Shows a one-off pill message (no rewrite behind it).</summary>
    private void ShowPill(string message, PillState state)
    {
        var caret = CaretPosition.Current();
        var pill = new PillWindow();
        pill.Show();
        pill.Start(caret.X, caret.Y);
        pill.ShowResult(state, message);
    }

    public void Dispose()
    {
        _hotkeys.Pressed -= OnPressed;
        _hotkeys.Dispose();
        _escape?.Dispose();
        _rewrite?.Cancel();
        _pill?.Close();
        _palette?.Close();
        _pill = null;
        _palette = null;
    }
}
