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

    /// <summary>Owns the clipboard while our text is on it, so Windows can ask it
    /// for the text when an app pastes (delayed rendering).</summary>
    private OwnerWindow? _owner;

    /// <summary>The text promised to the clipboard and not yet handed over.</summary>
    private string? _promisedText;

    /// <summary>
    /// An app has just read our text off the clipboard. Raised on the thread that
    /// called <see cref="PutTextForPaste"/>. Our text goes on as a promise
    /// (delayed rendering), so Windows tells us the moment a paste asks for it;
    /// the user's clipboard can go back after that rather than on a fixed timer,
    /// which a slow app could outlast and then paste the old clipboard
    /// (found by scripts/E2E, docs/PLAN.md P13.22).
    /// </summary>
    /// <para>The argument says whether the reader was the app the paste went to
    /// (the foreground app when the text went on). Background readers such as
    /// clipboard monitors also ask, sometimes first, and must not count as the
    /// paste.</para>
    public event EventHandler<bool>? TextRead;

    /// <summary>The process the paste is aimed at: the foreground app when our
    /// text went on.</summary>
    private uint _pasteTargetProcess;

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
    /// <returns>True when the user's clipboard went back; false when nothing was
    /// waiting or someone else had written to the clipboard since.</returns>
    public bool RestoreSaved()
    {
        if (!_restorePending)
        {
            return false;
        }

        _restorePending = false;
        var saved = _saved;
        _saved = null;

        // Still ours if nobody has written since: either the sequence number is
        // where we left it, or (after Windows asked us for the promised text,
        // which moves the number) our window still owns the clipboard.
        var ours = GetClipboardSequenceNumber() == _ownSequence
            || (_owner is not null && GetClipboardOwner() == _owner.Handle);
        if (ours)
        {
            Restore(saved);
        }

        return ours;
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
    private bool SetOwnText(string text)
    {
        _owner ??= new OwnerWindow(this);
        if (!OpenClipboardWithRetries(_owner.Handle))
        {
            return false;
        }

        try
        {
            _ = EmptyClipboard();

            // A promise: no data yet. Windows sends WM_RENDERFORMAT to the owner
            // window when an app asks for the text (see OwnerWindow).
            _promisedText = text;
            _ = GetWindowThreadProcessId(GetForegroundWindow(), out _pasteTargetProcess);
            _ = SetClipboardData(CF_UNICODETEXT, IntPtr.Zero);
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                _promisedText = null;
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

    private static bool OpenClipboardWithRetries(IntPtr owner)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(owner))
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

    /// <summary>Hands the promised text over: inside WM_RENDERFORMAT the
    /// clipboard is already open for us; for WM_RENDERALLFORMATS we open it,
    /// and only while we still own it.</summary>
    private void Render(bool open)
    {
        var text = _promisedText;
        if (text is null || _owner is null)
        {
            return;
        }

        // Inside WM_RENDERFORMAT the reader holds the clipboard open, so its
        // window says who is asking.
        var reader = 0u;
        if (!open)
        {
            _ = GetWindowThreadProcessId(GetOpenClipboardWindow(), out reader);
        }

        if (open)
        {
            if (!OpenClipboard(_owner.Handle))
            {
                return;
            }

            if (GetClipboardOwner() != _owner.Handle)
            {
                _ = CloseClipboard();
                return;
            }
        }

        try
        {
            var handle = AllocateGlobal(Encoding.Unicode.GetByteCount(text + '\0'));
            if (handle != IntPtr.Zero && WriteGlobal(handle, Encoding.Unicode.GetBytes(text + '\0'))
                && SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero)
            {
                _ = GlobalFree(handle);
            }
        }
        finally
        {
            if (open)
            {
                _ = CloseClipboard();
            }
        }

        _promisedText = null;

        // Handing the text over counts as a clipboard change; it is still ours,
        // so the restore must not mistake it for someone else's copy.
        _ownSequence = GetClipboardSequenceNumber();
        TextRead?.Invoke(this, reader != 0 && reader == _pasteTargetProcess);
    }

    /// <summary>A message-only window on the calling (UI) thread that owns the
    /// clipboard while our text is promised on it.</summary>
    private sealed class OwnerWindow : NativeWindow
    {
        private const int WM_RENDERFORMAT = 0x0305;
        private const int WM_RENDERALLFORMATS = 0x0306;
        private const int WM_DESTROYCLIPBOARD = 0x0307;

        private readonly ClipboardService _service;

        public OwnerWindow(ClipboardService service)
        {
            _service = service;
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_RENDERFORMAT when (uint)m.WParam == CF_UNICODETEXT:
                    _service.Render(open: false);
                    m.Result = IntPtr.Zero;
                    return;
                case WM_RENDERALLFORMATS:
                    // Going away with the text still promised: hand it over.
                    _service.Render(open: true);
                    m.Result = IntPtr.Zero;
                    return;
                case WM_DESTROYCLIPBOARD:
                    // Someone else emptied the clipboard; the promise is void.
                    _service._promisedText = null;
                    break;
            }

            base.WndProc(ref m);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern IntPtr GetOpenClipboardWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// <summary>Restores the clipboard when the paste is done.</summary>
    private sealed class RestoredClipboard(ClipboardService owner) : IDisposable
    {
        public void Dispose() => _ = owner.RestoreSaved();
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