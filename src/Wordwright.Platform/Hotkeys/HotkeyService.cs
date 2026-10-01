using System.Runtime.InteropServices;
using System.Windows.Forms;
using Wordwright.Core.Actions;

namespace Wordwright.Platform.Hotkeys;

/// <summary>One hotkey Wordwright wants registered, identified by the action id
/// or the palette's fixed id.</summary>
public sealed record HotkeyRegistration(string Id, HotkeySpec Spec);

/// <summary>A hotkey was pressed; <paramref name="Id"/> is the registration it
/// belongs to.</summary>
public sealed class HotkeyPressedEventArgs(string id, HotkeySpec spec) : EventArgs
{
    public string Id { get; } = id;

    public HotkeySpec Spec { get; } = spec;
}

/// <summary>
/// Registers Wordwright's hotkeys on a hidden message-only window and raises
/// <see cref="Pressed"/> when one is used (docs/ARCHITECTURE.md → Hotkeys).
/// <para>
/// Registration happens at start-up and again whenever settings or actions
/// change: the caller passes the whole set each time and every combination is
/// re-registered. A combination another app owns is kept in the set but marked
/// unregistered (<see cref="IsRegistered"/> is false), so the UI can say who has
/// it and pressing it can show the pill message. It is never silently stolen.
/// </para>
/// <para>
/// Call <see cref="Apply"/> on the UI thread: <c>RegisterHotKey</c> binds to the
/// thread that owns the window, which is where <see cref="Pressed"/> then runs.
/// </para>
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, HotkeyRegistration> _registered = [];
    private readonly Dictionary<string, bool> _outcomes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HotkeySpec> _specs = new(StringComparer.Ordinal);

    private MessageWindow? _window;
    private int _nextId;
    private bool _disposed;

    /// <summary>A registered hotkey was used.</summary>
    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    /// <summary>Whether a registration id is currently registered with Windows.</summary>
    public bool IsRegistered(string id) => _outcomes.TryGetValue(id, out var registered) && registered;

    /// <summary>The spec for a registration id, whether or not it registered.</summary>
    public HotkeySpec? SpecOf(string id) => _specs.TryGetValue(id, out var spec) ? spec : null;

    /// <summary>
    /// Unregisters everything and registers <paramref name="registrations"/>.
    /// Unusable or reserved combinations are recorded as not registered without
    /// being attempted.
    /// </summary>
    public void Apply(IEnumerable<HotkeyRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var handle = EnsureWindow().Handle;

        UnregisterAll();

        foreach (var registration in registrations)
        {
            _specs[registration.Id] = registration.Spec;

            if (HotkeyRules.Validate(registration.Spec.ToString()) != HotkeyValidation.Ok)
            {
                _outcomes[registration.Id] = false;
                continue;
            }

            var id = ++_nextId;
            var virtualKey = VirtualKeys.For(registration.Spec.Key);

            var ok = RegisterHotKey(
                handle,
                id,
                VirtualKeys.Modifiers(registration.Spec.Modifiers),
                virtualKey);

            _outcomes[registration.Id] = ok;
            if (ok)
            {
                _registered[id] = registration;
            }
        }
    }

    private MessageWindow EnsureWindow() => _window ??= new MessageWindow(OnHotkey);

    private void OnHotkey(int id)
    {
        if (_registered.TryGetValue(id, out var registration))
        {
            Pressed?.Invoke(this, new HotkeyPressedEventArgs(registration.Id, registration.Spec));
        }
    }

    private void UnregisterAll()
    {
        if (_window is null)
        {
            return;
        }

        foreach (var id in _registered.Keys)
        {
            _ = UnregisterHotKey(_window.Handle, id);
        }

        _registered.Clear();
        _outcomes.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();

        _window?.DestroyHandle();
        _window = null;
    }

    /// <summary>A hidden, message-only window that catches <c>WM_HOTKEY</c>. It
    /// never shows, so it is not a taskbar window.</summary>
    private sealed class MessageWindow : NativeWindow
    {
        private const int WM_HOTKEY = 0x0312;

        /// <summary>HWND_MESSAGE: a window that only receives messages.</summary>
        private static readonly IntPtr MessageOnlyParent = new(-3);

        private readonly Action<int> _onHotkey;

        internal MessageWindow(Action<int> onHotkey)
        {
            _onHotkey = onHotkey;

            CreateHandle(new CreateParams
            {
                Caption = "Wordwright hotkeys",
                Parent = MessageOnlyParent,
                Style = 0,
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
            });
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_HOTKEY)
            {
                _onHotkey(message.WParam.ToInt32());
            }

            base.WndProc(ref message);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
