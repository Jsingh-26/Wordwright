using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wordwright.Platform.Keyboard;

/// <summary>
/// Watches for a single Escape press while a rewrite is running, so Esc can
/// cancel it (docs/PLAN.md P6.6, docs/UX_COPY.md → Pill.Cancel). The pill never
/// takes focus, so a global watcher is the only way to hear the key; it is
/// started for the length of a rewrite and stopped afterwards.
/// <para>
/// It is separate from <see cref="KeyboardHook"/> so cancellation works even when
/// snippets are switched off. It reports key-down only, ignores injected input,
/// and never touches the clipboard or disk.
/// </para>
/// </summary>
public sealed class EscapeWatcher : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint WM_QUIT = 0x0012;
    private const uint VK_ESCAPE = 0x1B;
    private const uint LLKHF_INJECTED = 0x00000010;

    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private volatile bool _escaped;

    /// <summary>Escape was pressed. Raised on the watcher's own thread.</summary>
    public event EventHandler? Pressed;

    /// <summary>Installs the watcher and starts listening.</summary>
    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _escaped = false;
        _ready.Reset();

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Wordwright escape watcher",
        };
        _thread.Start();

        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }

        if (_threadId != 0)
        {
            _ = PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, OnKey, GetModuleHandle(null), 0);
        _ready.Set();

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            _ = TranslateMessage(ref message);
            _ = DispatchMessage(ref message);
        }

        if (_hook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    /// <summary>Runs on the hook thread: queue nothing, just react and return.</summary>
    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0
            && !_escaped
            && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KeyboardMessage>(lParam);
            if (data.VirtualKey == VK_ESCAPE && (data.Flags & LLKHF_INJECTED) == 0)
            {
                _escaped = true;
                // Off the hook thread so a slow handler cannot hold up the hook.
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Pressed?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine($"EscapeWatcher handler failed: {exception}");
                    }
                });
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
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

    private delegate IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

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
