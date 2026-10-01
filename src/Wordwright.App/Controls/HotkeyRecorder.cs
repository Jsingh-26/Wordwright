using System.Windows;
using System.Windows.Input;
using Wordwright.App.Resources;
using Wordwright.Core.Actions;

namespace Wordwright.App.Controls;

/// <summary>
/// A hotkey recorder (docs/DESIGN.md §3, docs/ARCHITECTURE.md → Hotkeys): click
/// it, press a combination, and it shows what was pressed — or why that will not
/// do. Esc cancels recording, Backspace clears the hotkey, and the value is kept
/// in <see cref="Hotkey"/>.
/// </summary>
public sealed class HotkeyRecorder : Wpf.Ui.Controls.Button
{
    private static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyRecorder),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    /// <summary>The combination as stored (<c>Ctrl+Alt+G</c>), or null for none.</summary>
    public string? Hotkey
    {
        get => (string?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>How the owner validates a candidate; returns the message to show
    /// and whether it was accepted. Null means accepted.</summary
    public Func<HotkeySpec, (bool Accepted, string? Message)>? Validate { get; set; }

    private bool _recording;
    private string? _beforeRecording;

    public HotkeyRecorder()
    {
        // Buttons are not focusable by default in WPF-UI's templates; the
        // recorder needs focus so it can hear the key press.
        Focusable = true;
        PreviewKeyDown += OnPreviewKeyDown;
        LostKeyboardFocus += (_, _) => StopRecording(restore: true);

        Refresh();
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyRecorder)d).Refresh();

    protected override void OnClick()
    {
        base.OnClick();
        StartRecording();
    }

    private void StartRecording()
    {
        _recording = true;
        _beforeRecording = Hotkey;
        Content = Strings.Get("Actions.Hotkey.Record");
        Focus();
    }

    private void StopRecording(bool restore)
    {
        if (!_recording)
        {
            return;
        }

        _recording = false;

        if (restore)
        {
            Hotkey = _beforeRecording;
        }

        Refresh();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording)
        {
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            StopRecording(restore: true);
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            Hotkey = null;
            _recording = false;
            Refresh();
            return;
        }

        // Ignore a lone modifier; wait for the real key.
        if (IsModifier(e.Key))
        {
            return;
        }

        var spec = FromKey(e);
        if (spec is null)
        {
            return;
        }

        if (HotkeyRules.Validate(spec.ToString()) == HotkeyValidation.Reserved)
        {
            // Keep recording so the user can pick another combination.
            Content = Strings.Get("Actions.Hotkey.Reserved", ("Hotkey", spec.ToString()));
            return;
        }

        if (Validate is { } validate)
        {
            var (accepted, message) = validate(spec);
            if (!accepted)
            {
                Content = message ?? Strings.Get("Actions.Hotkey.Invalid");
                return;
            }
        }

        _recording = false;
        Hotkey = spec.ToString();
        Refresh();
    }

    private static HotkeySpec? FromKey(KeyEventArgs e)
    {
        var modifiers = HotkeyModifiers.None;
        var keyboard = Keyboard.Modifiers;

        if (keyboard.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (keyboard.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (keyboard.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (keyboard.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            return null;
        }

        // System keys (Alt+…) arrive as Key.System; the real key is in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var name = KeyName(key);

        return name is null ? null : new HotkeySpec { Modifiers = modifiers, Key = name };
    }

    private static string? KeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z || key is >= Key.D0 and <= Key.D9)
        {
            return key.ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => null,
        };
    }

    private static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    /// <summary>Puts the current value (or "No hotkey") on the button.</summary>
    private void Refresh() =>
        Content = Hotkey is { Length: > 0 } value ? value : Strings.Get("Actions.Hotkey.None");
}
