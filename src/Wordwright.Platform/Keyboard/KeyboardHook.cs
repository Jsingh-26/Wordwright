using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Wordwright.Platform.Keyboard;

/// <summary>
/// Watches typing everywhere on the desktop (docs/ARCHITECTURE.md → Snippet
/// engine, steps 1–3). It reports printable characters, backspaces and the
/// moments the caller should forget what was typed: Enter, Esc, Tab, arrows,
/// Home/End, PgUp/PgDn, Delete, a mouse click, a change of foreground window,
/// and any typing in an excluded app.
/// <para>
/// No text is ever written to disk or to a log; the characters are handed to
/// the caller and forgotten.
/// </para>
/// <para>
/// The low-level hook runs on its own thread and only queues what it saw, so it
/// returns immediately; a second thread does the translating and raises the
/// events. Events therefore arrive on a background thread — the caller must
/// marshal them (the app hops to the UI thread).
/// </para>
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const uint WM_QUIT = 0x0012;
    private const uint LLKHF_INJECTED = 0x00000010;
    private const uint LLMHF_INJECTED = 0x00000001;

    private const uint VK_BACK = 0x08;
    private const uint VK_TAB = 0x09;
    private const uint VK_RETURN = 0x0D;
    private const uint VK_ESCAPE = 0x1B;
    private const uint VK_PRIOR = 0x21;
    private const uint VK_NEXT = 0x22;
    private const uint VK_END = 0x23;
    private const uint VK_HOME = 0x24;
    private const uint VK_LEFT = 0x25;
    private const uint VK_UP = 0x26;
    private const uint VK_RIGHT = 0x27;
    private const uint VK_DOWN = 0x28;
    private const uint VK_DELETE = 0x2E;
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_CAPITAL = 0x14;

    // ToUnicodeEx flag (Windows 10 1607+): translate without touching the
    // kernel's keyboard state, so a dead key the user is typing (^ or ´ on many
    // European layouts) still reaches the app intact.
    private const uint ToUnicodeKeepState = 0x4;

    private readonly IReadOnlyList<string> _excludedApps;
    private readonly ManualResetEventSlim _hookReady = new(false);
    private BlockingCollection<HookEvent> _events = new();

    private Thread? _hookThread;
    private Thread? _worker;
    private uint _hookThreadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private IntPtr _lastForeground;
    private volatile bool _installed;
    private bool _foregroundIsExcluded;
    private readonly char[] _translation = new char[8];

    /// <param name="excludedApps">Executable file names (e.g. <c>KeePass.exe</c>)
    /// where Wordwright stays quiet, from the user's settings.</param>
    /// <param name="ignoreInjectedInput">Low-level input that other programs
    /// synthesise — including Wordwright's own backspaces and paste — is ignored,
    /// so the hook cannot react to itself. Kept as a switch for the diagnostics
    /// harness, which has no other way to feed the hook.</param>
    public KeyboardHook(IEnumerable<string> excludedApps, bool ignoreInjectedInput = true)
    {
        _excludedApps = excludedApps.ToList();
        IgnoreInjectedInput = ignoreInjectedInput;
    }

    /// <summary>Whether synthesised input is filtered out (the production default).</summary>
    internal bool IgnoreInjectedInput { get; }

    /// <summary>A printable character was typed. Raised on a background thread.</summary>
    public event EventHandler<char>? CharacterTyped;

    /// <summary>A backspace was pressed: drop the last buffered character.
    /// Raised on a background thread.</summary>
    public event EventHandler? BackspacePressed;

    /// <summary>Forget everything buffered. Raised on a background thread.</summary>
    public event EventHandler? BufferCleared;

    public bool IsRunning => _hookThread is not null;

    /// <summary>True once Windows accepted the hooks. A refusal (another hook
    /// holder at the timeout, or a locked-down session) leaves this false.</summary>
    public bool IsInstalled => _installed;

    /// <summary>Installs the hooks on their own thread and starts reporting.</summary>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        // Stop() completes the old queue, so a restarted hook needs a fresh one.
        _hookReady.Reset();
        _events = new BlockingCollection<HookEvent>();
        _lastForeground = IntPtr.Zero;
        _foregroundIsExcluded = false;

        _hookThread = new Thread(HookThread)
        {
            IsBackground = true,
            Name = "Wordwright keyboard hook",
        };
        _hookThread.Start();

        _worker = new Thread(Work)
        {
            IsBackground = true,
            Name = "Wordwright keystroke worker",
        };
        _worker.Start();

        _hookReady.Wait(TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        if (_hookThread is null)
        {
            return;
        }

        // Let the worker drain what is queued, then end the hook thread's loop.
        _events.CompleteAdding();

        if (_hookThreadId != 0)
        {
            _ = PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _hookThread.Join(TimeSpan.FromSeconds(2));
        _worker?.Join(TimeSpan.FromSeconds(2));
        _hookThread = null;
        _worker = null;
    }

    public void Dispose()
    {
        Stop();
        _events.Dispose();
        _hookReady.Dispose();
    }

    private void HookThread()
    {
        _hookThreadId = GetCurrentThreadId();

        var module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, OnKeyboardMessage, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, OnMouseMessage, module, 0);
        _installed = _keyboardHook != IntPtr.Zero;
        _hookReady.Set();

        // A low-level hook needs its thread to pump messages; it does no work here.
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            _ = TranslateMessage(ref message);
            _ = DispatchMessage(ref message);
        }

        if (_mouseHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _installed = false;
    }

    /// <summary>Runs on the hook thread: queue and return, nothing else.</summary>
    private IntPtr OnKeyboardMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KeyboardMessage>(lParam);
            if (!IsInjected(data.Flags, LLKHF_INJECTED))
            {
                // Our own typing (backspaces, the paste) is injected and never lands here.
                // The modifiers are read now: by the time the worker translates the
                // key the user may have let go of Shift.
                _ = _events.TryAdd(HookEvent.Key(data.VirtualKey, data.ScanCode, ReadModifiers()));
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    /// <summary>Runs on the hook thread: queue the click and return.</summary>
    private IntPtr OnMouseMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0
            && (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN
                || wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN))
        {
            var data = Marshal.PtrToStructure<MouseMessage>(lParam);
            if (!IsInjected(data.Flags, LLMHF_INJECTED))
            {
                _ = _events.TryAdd(HookEvent.Forget());
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void Work()
    {
        foreach (var item in _events.GetConsumingEnumerable())
        {
            try
            {
                if (item.IsForget)
                {
                    BufferCleared?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    HandleKey(item.VirtualKey, item.ScanCode, item.Modifiers);
                }
            }
            catch (Exception exception)
            {
                // A handler failing must not take down the hook thread.
                Debug.WriteLine($"KeyboardHook handler failed: {exception}");
            }
        }
    }

    private void HandleKey(uint virtualKey, uint scanCode, Modifiers modifiers)
    {
        var foreground = GetForegroundWindow();

        if (foreground != _lastForeground)
        {
            // A different window has the caret, so anything buffered is stale.
            _lastForeground = foreground;
            _foregroundIsExcluded = IsExcluded(foreground);
            BufferCleared?.Invoke(this, EventArgs.Empty);

            if (_foregroundIsExcluded)
            {
                return;
            }
        }
        else if (_foregroundIsExcluded)
        {
            return;
        }

        if (IsClearKey(virtualKey))
        {
            BufferCleared?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsModifier(virtualKey))
        {
            return;
        }

        if (IsBackspace(virtualKey))
        {
            BackspacePressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Translate(virtualKey, scanCode, modifiers, foreground) is { } character)
        {
            CharacterTyped?.Invoke(this, character);
        }
    }

    /// <summary>Turns a key into the character it would produce in the foreground
    /// window, using that window's keyboard layout and the modifiers held when it
    /// was pressed. Control characters (a Ctrl combination, say) and dead keys
    /// produce nothing.</summary>
    private char? Translate(uint virtualKey, uint scanCode, Modifiers modifiers, IntPtr foreground)
    {
        var threadId = GetWindowThreadProcessId(foreground, out _);
        var layout = GetKeyboardLayout(threadId);

        // GetKeyboardState cannot be used here: this thread has no input of its
        // own, so its key state never shows Shift, Caps Lock or AltGr. Build the
        // state from what the hook saw instead.
        var keyboardState = new byte[256];
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.LeftShift), VK_LSHIFT, VK_SHIFT);
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.RightShift), VK_RSHIFT, VK_SHIFT);
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.LeftControl), VK_LCONTROL, VK_CONTROL);
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.RightControl), VK_RCONTROL, VK_CONTROL);
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.LeftAlt), VK_LMENU, VK_MENU);
        SetDown(keyboardState, modifiers.HasFlag(Modifiers.RightAlt), VK_RMENU, VK_MENU);
        if (modifiers.HasFlag(Modifiers.CapsLock))
        {
            keyboardState[VK_CAPITAL] = 0x01;
        }

        // A negative value is a dead key; two characters are a ligature. Only a
        // single character is a plain typed character.
        var written = ToUnicodeEx(
            virtualKey, scanCode, keyboardState, _translation, _translation.Length, ToUnicodeKeepState, layout);

        if (written != 1)
        {
            return null;
        }

        var character = _translation[0];
        return char.IsControl(character) ? null : character;
    }

    private static void SetDown(byte[] state, bool down, uint sideKey, int key)
    {
        if (down)
        {
            state[sideKey] = 0x80;
            state[key] = 0x80;
        }
    }

    /// <summary>Runs on the hook thread, where the asynchronous key state already
    /// includes every key pressed before this one.</summary>
    private static Modifiers ReadModifiers()
    {
        var modifiers = Modifiers.None;
        if (IsDown(VK_LSHIFT)) modifiers |= Modifiers.LeftShift;
        if (IsDown(VK_RSHIFT)) modifiers |= Modifiers.RightShift;
        if (IsDown(VK_LCONTROL)) modifiers |= Modifiers.LeftControl;
        if (IsDown(VK_RCONTROL)) modifiers |= Modifiers.RightControl;
        if (IsDown(VK_LMENU)) modifiers |= Modifiers.LeftAlt;
        if (IsDown(VK_RMENU)) modifiers |= Modifiers.RightAlt;
        if ((GetKeyState(VK_CAPITAL) & 0x0001) != 0) modifiers |= Modifiers.CapsLock;
        return modifiers;
    }

    private static bool IsDown(uint virtualKey) => (GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;

    private bool IsInjected(uint flags, uint injectedFlag)
        => IgnoreInjectedInput && (flags & injectedFlag) != 0;

    private bool IsExcluded(IntPtr window)
    {
        if (_excludedApps.Count == 0 || window == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var fileName = process.ProcessName + ".exe";

            return _excludedApps.Any(excluded =>
                string.Equals(Path.GetFileName(excluded), fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (ArgumentException)
        {
            // The process ended between the click and this check.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsBackspace(uint virtualKey) => virtualKey == VK_BACK;

    private static bool IsClearKey(uint virtualKey) => virtualKey is
        VK_RETURN or VK_ESCAPE or VK_TAB or VK_DELETE
        or VK_LEFT or VK_UP or VK_RIGHT or VK_DOWN
        or VK_HOME or VK_END or VK_PRIOR or VK_NEXT;

    private static bool IsModifier(uint virtualKey) => virtualKey is
        VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL
        or VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN;

    [Flags]
    private enum Modifiers
    {
        None = 0,
        LeftShift = 1,
        RightShift = 2,
        LeftControl = 4,
        RightControl = 8,
        LeftAlt = 16,
        RightAlt = 32,
        CapsLock = 64,
    }

    private readonly record struct HookEvent(uint VirtualKey, uint ScanCode, Modifiers Modifiers, bool IsForget)
    {
        public static HookEvent Key(uint virtualKey, uint scanCode, Modifiers modifiers)
            => new(virtualKey, scanCode, modifiers, false);

        public static HookEvent Forget() => new(0, 0, Modifiers.None, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardMessage
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseMessage
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private delegate IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    // CharSet.Unicode matters: ToUnicodeEx writes UTF-16, and the default ANSI
    // marshalling of a char[] would turn every character into garbage.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey, uint scanCode, byte[] state, char[] buffer, int bufferSize, uint flags, IntPtr layout);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint min, uint max);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }
}