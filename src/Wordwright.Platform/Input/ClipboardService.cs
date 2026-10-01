using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Wordwright.Platform.Input;

/// <summary>
/// Puts text on the clipboard for a paste and puts back whatever was there
/// afterwards (docs/ARCHITECTURE.md → Paste and selection capture).
/// <para>
/// Our own text is marked so Windows keeps it out of clipboard history (Win+V)
/// and out of cloud clipboard sync. The saved clipboard is restored best-effort:
/// every format we can copy is copied, and a format another program refuses to
/// hand over is simply not restored.
/// </para>
/// <para>
/// The Windows clipboard is an OLE object, so these calls must come from a
/// single-threaded-apartment thread — in the app, the UI thread.
/// </para>
/// </summary>
public sealed class ClipboardService
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>Windows checks these to decide whether clipboard content may be
    /// recorded by Win+V history and synced to the cloud; a DWORD of 0 means no.
    /// The first is the one docs/ARCHITECTURE.md names.</summary>
    private static readonly string[] HistoryExclusionFormats =
    [
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
    ];

    /// <summary>The clipboard's text, or null when it holds none. Used for the
    /// <c>{clipboard}</c> variable.</summary>
    public string? GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException)
        {
            // Another program is holding the clipboard; treat it as empty.
            return null;
        }
    }

    /// <summary>True when the clipboard holds text, and it is not empty.</summary>
    public bool ContainsNonEmptyText()
    {
        try
        {
            return Clipboard.ContainsText() && Clipboard.GetText().Length > 0;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    /// <summary>
    /// Empties the clipboard and hands back what was there, so a selection capture
    /// can tell a real copy from an unchanged clipboard
    /// (docs/ARCHITECTURE.md → Capture selection).
    /// </summary>
    public object? CaptureForSelection()
    {
        var saved = CaptureCurrent();

        try
        {
            Clipboard.Clear();
        }
        catch (ExternalException)
        {
            // Another program is holding it; the poll will simply see old text.
        }

        return saved;
    }

    /// <summary>Puts the clipboard back after a selection capture.</summary>
    public void RestoreAfterSelection(object? saved) => Restore(saved);

    /// <summary>
    /// Replaces the clipboard with <paramref name="text"/>, marked to stay out of
    /// clipboard history, and remembers what was there. Disposing puts the old
    /// contents back, so call it as soon as the paste has gone through.
    /// </summary>
    public IDisposable ReplaceWithText(string text)
    {
        var saved = CaptureCurrent();
        SetOwnText(text);

        return new RestoredClipboard(saved);
    }

    /// <summary>
    /// Keeps <paramref name="text"/> on the clipboard without a later restore
    /// (the elevated-app fallback, docs/PLAN.md P6.6 → copy fallback).
    /// </summary>
    public void SetText(string text) => SetOwnText(text);

    private static object? CaptureCurrent()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current is null)
            {
                return null;
            }

            // Copy what we can into our own data object: the proxy we just got
            // belongs to the clipboard, which we are about to change.
            var copy = new DataObject();
            foreach (var format in current.GetFormats())
            {
                try
                {
                    if (current.GetData(format) is { } data)
                    {
                        copy.SetData(format, data);
                    }
                }
                catch (ExternalException)
                {
                    // Best effort: this format (often an OLE one another program
                    // will not hand over) stays unrestored.
                }
            }

            return copy;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static void Restore(object? saved)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (saved is null)
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetDataObject(saved, copy: true);
                }

                return;
            }
            catch (ExternalException)
            {
                // The clipboard is briefly locked by whoever just pasted.
                Thread.Sleep(20);
            }
        }
    }

    /// <summary>
    /// Writes the text and the history-exclusion markers straight through the
    /// Win32 clipboard, so the marker payload is exactly the DWORD Windows reads.
    /// </summary>
    private static void SetOwnText(string text)
    {
        if (!OpenClipboardWithRetries())
        {
            return;
        }

        try
        {
            _ = EmptyClipboard();

            var textHandle = AllocateGlobal(Encoding.Unicode.GetByteCount(text + '\0'));
            if (textHandle != IntPtr.Zero && WriteGlobal(textHandle, Encoding.Unicode.GetBytes(text + '\0')))
            {
                if (SetClipboardData(CF_UNICODETEXT, textHandle) == IntPtr.Zero)
                {
                    _ = GlobalFree(textHandle);
                }
            }

            foreach (var format in HistoryExclusionFormats)
            {
                var formatId = RegisterClipboardFormat(format);
                if (formatId == 0)
                {
                    continue;
                }

                var markerHandle = AllocateGlobal(sizeof(uint));
                if (markerHandle == IntPtr.Zero || !WriteGlobal(markerHandle, new byte[sizeof(uint)]))
                {
                    continue;
                }

                if (SetClipboardData(formatId, markerHandle) == IntPtr.Zero)
                {
                    _ = GlobalFree(markerHandle);
                }
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    private static bool OpenClipboardWithRetries()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    private static IntPtr AllocateGlobal(int bytes) => GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);

    private static bool WriteGlobal(IntPtr handle, byte[] bytes)
    {
        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return false;
        }

        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        _ = GlobalUnlock(handle);
        return true;
    }

    /// <summary>Restores the clipboard when the paste is done.</summary>
    private sealed class RestoredClipboard : IDisposable
    {
        private readonly object? _saved;
        private bool _disposed;

        public RestoredClipboard(object? saved)
        {
            _saved = saved;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Restore(_saved);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}