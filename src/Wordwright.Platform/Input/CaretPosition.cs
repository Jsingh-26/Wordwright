using System.Runtime.InteropServices;

namespace Wordwright.Platform.Input;

/// <summary>Where to put the palette and the pill: at the caret when Windows can
/// tell us, otherwise at the mouse (docs/DESIGN.md §6, docs/PLAN.md P6.5).</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>
/// The caret's screen position in the foreground app
/// (docs/ARCHITECTURE.md → AI rewrite flow step 4: "near the caret, fallback:
/// near the mouse"). Many apps do not report a caret to
/// <c>GetGUIThreadInfo</c>, so the mouse is a normal answer, not an error.
/// </summary>
public static class CaretPosition
{
    /// <summary>The caret's screen point, or the mouse when there is no caret.</summary>
    public static ScreenPoint Current()
    {
        var window = GetForegroundWindow();
        if (CaretPoint(window) is { } caret)
        {
            return caret;
        }

        return GetCursorPos(out var point) ? new ScreenPoint(point.X, point.Y) : new ScreenPoint(0, 0);
    }

    private static ScreenPoint? CaretPoint(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return null;
        }

        var threadId = GetWindowThreadProcessId(window, out _);
        if (threadId == 0)
        {
            return null;
        }

        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(threadId, ref info) || info.CaretWindow == IntPtr.Zero)
        {
            return null;
        }

        // The caret rectangle is in the caret window's client coordinates.
        var point = new NativePoint
        {
            X = info.CaretRect.Right,
            Y = info.CaretRect.Bottom,
        };

        return ClientToScreen(info.CaretWindow, ref point)
            ? new ScreenPoint(point.X, point.Y)
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public NativeRect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
}
