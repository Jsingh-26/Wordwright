using System.Runtime.InteropServices;

namespace Wordwright.Platform.Input;

/// <summary>
/// The foreground window, remembered and restored around a flow that has to take
/// focus for a moment (the palette) and then act on the app the user was in.
/// </summary>
public static class ForegroundWindow
{
    /// <summary>The current foreground window, to be given back later.</summary>
    public static IntPtr Current() => GetForegroundWindow();

    /// <summary>Gives focus back to a remembered window.</summary>
    public static void Restore(IntPtr window)
    {
        if (window != IntPtr.Zero)
        {
            _ = SetForegroundWindow(window);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
