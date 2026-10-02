using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Wordwright.Platform.Input;

/// <summary>
/// Puts text on the clipboard for a paste and puts back whatever was there
/// afterwards (docs/ARCHITECTURE.md → Paste).
/// <para>
/// Our own text is marked so Windows keeps it out of clipboard history (Win+V)
/// and out of cloud clipboard sync. The saved clipboard is restored best-effort
/// and cheaply (docs/PLAN.md P12.7): only the everyday formats are kept (text,
/// rich text, HTML, CSV, files, and a picture when there is no text), within a
/// small time budget, because a copied Excel range or Word paragraph offers
/// dozens of formats that each take time to render.
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

    /// <summary>Past this, no further formats are copied; what was captured so
    /// far (text first) is what comes back.</summary>
    private static readonly TimeSpan CaptureBudget = TimeSpan.FromMilliseconds(200);

    /// <summary>The formats worth restoring, most important first.</summary>
    private static readonly string[] RestoredFormats =
    [
        DataFormats.UnicodeText,
        DataFormats.Text,
        DataFormats.Rtf,
        DataFormats.Html,
        DataFormats.CommaSeparatedValue,
        DataFormats.FileDrop,
    ];

    /// <summary>Windows checks these to decide whether clipboard content may be
    /// recorded by Win+V history and synced to the cloud; a DWORD of 0 means no.
    /// The first is the one docs/ARCHITECTURE.md names.</summary>
    private static readonly string[] HistoryExclusionFormats =
    [
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
    ];

    /// <summary>The user's clipboard while our text is on it.</summary>
    private DataObject? _saved;

    /// <summary>True between <see cref="PutTextForPaste"/> and the restore.</summary>
    private bool _restorePending;

    /// <summary>The clipboard sequence number right after our text went on, so a
    /// restore can tell whether anyone has written to the clipboard since.</summary>
    private uint _ownSequence;

    /// <summary>The user's clipboard text, or null when it holds none. Used for
    /// the <c>{clipboard}</c> variable; while our own text is waiting to be
    /// replaced, it answers from what the user had copied.</summary>
    public string? GetText()
    {
        if (_restorePending)
        {
            return _saved is not null && _saved.TryGetData<string>(DataFormats.UnicodeText, out var saved)
                ? saved
                : null;
        }

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

    /// <summary>
    /// Replaces the clipboard with <paramref name="text"/>, marked to stay out of
    /// clipboard history, remembering what was there. If an earlier paste has not
    /// been restored yet, the clipboard still holds our text, so the user's
    /// original is kept rather than captured again.
    /// </summary>
    /// <returns>False when another program held the clipboard and our text did
    /// not go on; a paste now would insert whatever is there instead
    /// (docs/PLAN.md P13.1).</returns>
    public bool PutTextForPaste(string text)
    {
        var captured = false;
        if (!_restorePending)
        {
            _saved = CaptureCurrent();
            _restorePending = true;
            captured = true;
        }

        if (!SetOwnText(text))
        {
            if (captured)
            {
                // Nothing of ours went on, so there is nothing to put back.
                _restorePending = false;
                _saved = null;
            }

            return false;
        }

        _ownSequence = GetClipboardSequenceNumber();
        return true;
    }

    /// <summary>
    /// Puts the user's clipboard back, unless something else has written to it
    /// since our text went on: that is newer than what we saved, so it stays.
    /// </summary>
    public void RestoreSaved()
    {
        if (!_restorePending)
        {
            return;
        }

        _restorePending = false;
        var saved = _saved;
        _saved = null;

        if (GetClipboardSequenceNumber() == _ownSequence)
        {
            Restore(saved);
        }
    }

    /// <summary>
    /// The synchronous form, for the diagnostics harness: replaces the clipboard
    /// and restores it when the result is disposed.
    /// </summary>
    public IDisposable ReplaceWithText(string text)
    {
        _ = PutTextForPaste(text);
        return new RestoredClipboard(this);
    }

    private static DataObject? CaptureCurrent()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current is null)
            {
                return null;
            }

            // Copy into our own data object: the proxy we just got belongs to the
            // clipboard, which we are about to change.
            var copy = new DataObject();
            var clock = Stopwatch.StartNew();
            var hasText = false;

            foreach (var format in RestoredFormats)
            {
                if (clock.Elapsed > CaptureBudget)
                {
                    break;
                }

                if (TryCopy(current, copy, format)
                    && (format == DataFormats.UnicodeText || format == DataFormats.Text))
                {
                    hasText = true;
                }
            }

            // A picture is only worth its render cost when it is the content (a
            // screenshot), not one more view of copied cells or text.
            if (!hasText && clock.Elapsed <= CaptureBudget)
            {
                _ = TryCopy(current, copy, DataFormats.Bitmap);
            }

            return copy;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static bool TryCopy(IDataObject from, DataObject to, string format)
    {
        try
        {
            if (from.GetDataPresent(format, autoConvert: false)
                && from.GetData(format, autoConvert: false) is { } data)
            {
                to.SetData(format, data);
                return true;
            }
        }
        catch (ExternalException)
        {
            // Best effort: a format another program will not hand over stays
            // unrestored.
        }

        return false;
    }

    private static void Restore(DataObject? saved)
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
    /// Returns false when the text did not go on.
    /// </summary>
    private static bool SetOwnText(string text)
    {
        if (!OpenClipboardWithRetries())
        {
            return false;
        }

        try
        {
            _ = EmptyClipboard();

            var textSet = false;
            var textHandle = AllocateGlobal(Encoding.Unicode.GetByteCount(text + '\0'));
            if (textHandle != IntPtr.Zero && WriteGlobal(textHandle, Encoding.Unicode.GetBytes(text + '\0')))
            {
                if (SetClipboardData(CF_UNICODETEXT, textHandle) == IntPtr.Zero)
                {
                    _ = GlobalFree(textHandle);
                }
                else
                {
                    textSet = true;
                }
            }

            if (!textSet)
            {
                return false;
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

            return true;
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
    private sealed class RestoredClipboard(ClipboardService owner) : IDisposable
    {
        public void Dispose() => owner.RestoreSaved();
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

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