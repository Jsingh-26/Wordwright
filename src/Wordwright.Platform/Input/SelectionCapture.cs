using System.Runtime.InteropServices;

namespace Wordwright.Platform.Input;

/// <summary>
/// Reads the text the user has selected in the foreground app
/// (docs/ARCHITECTURE.md → Capture selection). The clipboard is saved, cleared,
/// Ctrl+C is sent, the new text is polled for up to 400 ms, then the clipboard is
/// put back. An empty answer means nothing was selected — never that the copy
/// failed, which is why capture is best-effort here and the caller decides.
/// </summary>
public static class SelectionCapture
{
    /// <summary>How long to wait for the target app to answer the copy.</summary>
    private const int PollMilliseconds = 400;

    private const int PollIntervalMilliseconds = 20;

    /// <summary>
    /// The selected text, or null when nothing was captured. Must be called on a
    /// single-threaded-apartment thread (the app's UI thread), because it touches
    /// the clipboard.
    /// </summary>
    public static string? Capture(ClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        // Save the clipboard, then make it empty so an unchanged copy cannot look
        // like a selection.
        var saved = clipboard.CaptureForSelection();

        InputSender.Copy();

        var text = PollForText(clipboard);

        clipboard.RestoreAfterSelection(saved);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? PollForText(ClipboardService clipboard)
    {
        var waited = 0;

        while (waited < PollMilliseconds)
        {
            if (clipboard.ContainsNonEmptyText())
            {
                return clipboard.GetText();
            }

            Thread.Sleep(PollIntervalMilliseconds);
            waited += PollIntervalMilliseconds;
        }

        return null;
    }
}

/// <summary>
/// Whether the foreground window belongs to a process running elevated
/// (docs/ARCHITECTURE.md → Paste and selection capture → Known limitation).
/// Windows blocks a normal app's input into an elevated one, so Wordwright warns
/// instead of appearing to fail silently.
/// </summary>
public static class ElevationCheck
{
    /// <summary>True when the foreground window's process is elevated.</summary>
    public static bool ForegroundIsElevated() => IsElevated(GetForegroundWindow());

    /// <summary>True when the window's process runs at a higher integrity level.</summary>
    public static bool IsElevated(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return false;
        }

        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return TokenIsElevated(process);
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    private static bool TokenIsElevated(IntPtr process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token))
        {
            return false;
        }

        try
        {
            var size = Marshal.SizeOf<TokenElevation>();
            var buffer = Marshal.AllocHGlobal(size);

            try
            {
                if (!GetTokenInformation(token, TokenElevationClass, buffer, size, out _))
                {
                    return false;
                }

                return Marshal.PtrToStructure<TokenElevation>(buffer).TokenIsElevated != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            _ = CloseHandle(token);
        }
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevationClass = 20;

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr token, int informationClass, IntPtr information, int length, out int returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
