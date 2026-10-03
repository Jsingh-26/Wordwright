using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Wordwright.Platform.Keyboard;

namespace E2E;

/// <summary>Win32 calls the runner needs: tagged input, windows, layouts, captures.</summary>
internal static class Native
{
    public static readonly IntPtr Tag = (IntPtr)KeyboardHook.TestInputTag;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Vk; public ushort Scan; public uint Flags; public uint Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int Dx; public int Dy; public uint Data; public uint Flags; public uint Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public KeyboardInput Ki; [FieldOffset(0)] public MouseInput Mi; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion U; }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint n, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr hkl);
    [DllImport("user32.dll")] public static extern short VkKeyScanEx(char ch, IntPtr hkl);
    [DllImport("user32.dll")] public static extern IntPtr LoadKeyboardLayout(string klid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnloadKeyboardLayout(IntPtr hkl);
    [DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
    [DllImport("user32.dll")] public static extern int GetKeyboardLayoutList(int n, IntPtr[]? list);
    [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr h, bool altTab);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint to, bool on);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr h);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int L, T, R, B; public int W => R - L; public int H => B - T; public override string ToString() => $"{L},{T} {W}x{H}"; }

    public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_RMENU = 0xA5, VK_LCONTROL = 0xA2,
        VK_BACK = 0x08, VK_RETURN = 0x0D, VK_ESCAPE = 0x1B, VK_TAB = 0x09, VK_DELETE = 0x2E, VK_CAPITAL = 0x14,
        VK_LWIN = 0x5B, VK_HOME = 0x24, VK_END = 0x23;

    public static void MakeDpiAware() => SetProcessDpiAwarenessContext((IntPtr)(-4));

    /// <summary>Safety guard, set by the runner: true when the foreground window
    /// belongs to something the runner started. Every key press checks it first,
    /// so if the person at the PC clicks into their own window mid-run, the
    /// runner stops instead of typing into it.</summary>
    public static Func<(bool ok, string who)>? ForegroundGuard;

    /// <summary>One key down or up, stamped with the test tag.</summary>
    public static void Key(ushort vk, bool up, IntPtr hkl)
    {
        if (!up && ForegroundGuard is not null)
        {
            var (ok, who) = ForegroundGuard();
            if (!ok) throw new ForeignWindowException(who);
        }

        var scan = (ushort)MapVirtualKeyEx(vk, 0, hkl);
        uint flags = up ? 0x0002u : 0;
        if (vk is VK_RMENU or 0x2E or 0x24 or 0x23 or 0x25 or 0x26 or 0x27 or 0x28) flags |= 0x0001; // extended keys
        var input = new Input { Type = 1, U = new InputUnion { Ki = new KeyboardInput { Vk = vk, Scan = scan, Flags = flags, Extra = Tag } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
            throw new InvalidOperationException("SendInput failed: " + Marshal.GetLastWin32Error());
        Thread.Sleep(12);
    }

    public static void Tap(ushort vk, IntPtr hkl = default) { Key(vk, false, hkl); Key(vk, true, hkl); }

    /// <summary>The Win key alone, which only opens Start and types into nothing,
    /// so it skips the foreground guard.</summary>
    public static void TapWindowsKey()
    {
        var guard = ForegroundGuard;
        ForegroundGuard = null;
        try { Tap(VK_LWIN); } finally { ForegroundGuard = guard; }
    }

    public static void Chord(IntPtr hkl, ushort modifier, ushort vk) { Key(modifier, false, hkl); Tap(vk, hkl); Key(modifier, true, hkl); }

    /// <summary>Types text the way a person on the given layout would press the keys:
    /// each character becomes its virtual key plus Shift, Ctrl+Alt (AltGr) as the layout needs.</summary>
    public static void Type(string text, IntPtr hkl)
    {
        foreach (var c in text)
        {
            var r = VkKeyScanEx(c, hkl);
            if (r == -1) throw new InvalidOperationException($"'{c}' cannot be typed on layout {hkl:X}");
            var vk = (ushort)(r & 0xFF);
            var mods = (r >> 8) & 0xFF;
            var shift = (mods & 1) != 0;
            var altGr = (mods & 6) == 6;
            if (altGr) { Key(VK_LCONTROL, false, hkl); Key(VK_RMENU, false, hkl); }
            if (shift) Key(VK_SHIFT, false, hkl);
            Tap(vk, hkl);
            if (shift) Key(VK_SHIFT, true, hkl);
            if (altGr) { Key(VK_RMENU, true, hkl); Key(VK_LCONTROL, true, hkl); }
        }
    }

    public static void Click(int x, int y, bool right = false)
    {
        SetCursorPos(x, y);
        Thread.Sleep(60);
        uint down = right ? 0x0008u : 0x0002u, up = right ? 0x0010u : 0x0004u;
        var a = new Input { Type = 0, U = new InputUnion { Mi = new MouseInput { Flags = down, Extra = Tag } } };
        var b = new Input { Type = 0, U = new InputUnion { Mi = new MouseInput { Flags = up, Extra = Tag } } };
        SendInput(1, [a], Marshal.SizeOf<Input>()); Thread.Sleep(40);
        SendInput(1, [b], Marshal.SizeOf<Input>()); Thread.Sleep(80);
    }

    /// <summary>Brings a window forward. Windows only lets the process that sent the
    /// last input do that, so when the plain call is refused the runner clicks the
    /// window's title bar, as a person would.</summary>
    public static void Front(IntPtr hwnd)
    {
        ShowWindow(hwnd, 9);
        SetForegroundWindow(hwnd);
        Thread.Sleep(200);
        if (GetForegroundWindow() != hwnd)
        {
            // The shell's own window switch (what Alt+Tab uses) is not subject to
            // the foreground lock that refuses SetForegroundWindow.
            SwitchToThisWindow(hwnd, true);
            Thread.Sleep(300);
        }

        if (GetForegroundWindow() != hwnd)
        {
            // Share the current foreground thread's input state for a moment:
            // Windows then accepts the switch from this thread.
            var fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var me = GetCurrentThreadId();
            AttachThreadInput(me, fgThread, true);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            AttachThreadInput(me, fgThread, false);
            Thread.Sleep(300);
        }
    }

    public static Bitmap Capture(Rect r)
    {
        var bmp = new Bitmap(Math.Max(1, r.W), Math.Max(1, r.H));
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(r.L, r.T, 0, 0, bmp.Size);
        return bmp;
    }

    public static string Save(Bitmap bmp, string dir, string name)
    {
        var path = Path.Combine(dir, name + ".png");
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
        return path;
    }

    public static IntPtr[] Layouts()
    {
        var n = GetKeyboardLayoutList(0, null);
        var list = new IntPtr[n];
        GetKeyboardLayoutList(n, list);
        return list;
    }
}

/// <summary>Thrown when the foreground window is not one the runner started.</summary>
internal sealed class ForeignWindowException(string who)
    : Exception("stopped before typing: the foreground window is " + who + ", which the runner did not start");
