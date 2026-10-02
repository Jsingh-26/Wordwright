using System.Runtime.InteropServices;
using System.Windows.Forms;
using Wordwright.Platform.Input;

namespace PasteHarness;

/// <summary>
/// Checks the paste path against a real text box: paste, backspaces, Left arrows,
/// the clipboard swap and its restore, and the markers that keep our text out of
/// Win+V history. The maintainer's clipboard text is saved and restored.
/// </summary>
internal static class Program
{
    private static readonly List<string> Failures = [];

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var form = new Form { Text = "Wordwright paste harness", Width = 520, Height = 200 };
        var box = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 12) };
        form.Controls.Add(box);
        form.Show();
        form.Activate();
        box.Focus();
        Application.DoEvents();

        var clipboard = new ClipboardService();
        var originalClipboard = clipboard.GetText();

        try
        {
            // 1. Paste our text into the box.
            using (clipboard.ReplaceWithText("hello"))
            {
                InputSender.Paste();
                Wait(300);

                Check("our text is on the clipboard", Clipboard.GetText() == "hello");
                Check("{clipboard} still reads the user's text", clipboard.GetText() == originalClipboard);
                Check("marked to stay out of Win+V history", HasHistoryExclusionMarkers());
            }

            Check("pasted text arrived", box.Text == "hello");

            // 2. Backspaces remove what was typed.
            InputSender.SendBackspaces(5);
            Wait(300);
            Check("backspaces cleared it", box.Text == "");

            // 3. Left arrows decide where the caret is, so the paste lands inside.
            using (clipboard.ReplaceWithText("abc"))
            {
                InputSender.Paste();
                Wait(200);
            }

            InputSender.SendLeftArrows(2);
            using (clipboard.ReplaceWithText("X"))
            {
                InputSender.Paste();
                Wait(300);
            }

            Check("Left arrows moved the caret into the text", box.Text == "aXbc");

            // 4. The clipboard is back to what the maintainer had.
            Wait(200);
            Check("clipboard restored", clipboard.GetText() == originalClipboard);

            // 5. Rich content comes back with its formats (P12.7).
            var rich = new DataObject();
            rich.SetData(DataFormats.UnicodeText, "rich");
            rich.SetData(DataFormats.Html, "<b>rich</b>");
            rich.SetData(DataFormats.Rtf, @"{\rtf1 rich}");
            Clipboard.SetDataObject(rich, copy: true);
            using (clipboard.ReplaceWithText("plain"))
            {
            }

            var back = Clipboard.GetDataObject();
            Check("text, HTML and RTF restored",
                back is not null
                && back.GetDataPresent(DataFormats.UnicodeText)
                && back.GetDataPresent(DataFormats.Html)
                && back.GetDataPresent(DataFormats.Rtf));

            // 6. A copy made while our text is on the clipboard is newer than
            // what was saved, so the restore leaves it alone (P12.7).
            clipboard.PutTextForPaste("ours");
            Clipboard.SetText("newer");
            clipboard.RestoreSaved();
            Check("a newer copy is not overwritten", Clipboard.GetText() == "newer");
        }
        finally
        {
            // Set directly: ReplaceWithText would restore whatever the last check
            // left on the clipboard, not the maintainer's text.
            clipboard.RestoreSaved();
            if (originalClipboard is not null)
            {
                Clipboard.SetText(originalClipboard);
            }
            else
            {
                Clipboard.Clear();
            }
        }

        foreach (var failure in Failures)
        {
            Console.WriteLine($"FAIL: {failure}");
        }

        Console.WriteLine(Failures.Count == 0 ? "PASS" : $"FAILED ({Failures.Count})");

        form.Close();
        Environment.Exit(Failures.Count == 0 ? 0 : 1);
    }

    private static bool HasHistoryExclusionMarkers()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            foreach (var name in new[]
                     {
                         "ExcludeClipboardContentFromMonitorProcessing",
                         "CanIncludeInClipboardHistory",
                         "CanUploadToCloudClipboard",
                     })
            {
                var format = RegisterClipboardFormat(name);
                if (format == 0 || !IsClipboardFormatAvailable(format))
                {
                    Console.WriteLine($"  missing marker: {name}");
                    return false;
                }

                // Windows reads a DWORD: anything but 0 leaves the content recordable.
                var handle = GetClipboardData(format);
                var pointer = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    Console.WriteLine($"  marker has no data: {name}");
                    return false;
                }

                var value = Marshal.ReadInt32(pointer);
                _ = GlobalUnlock(handle);

                if (value != 0)
                {
                    Console.WriteLine($"  marker is {value}, not 0: {name}");
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    private static void Check(string what, bool passed)
    {
        Console.WriteLine($"{(passed ? "ok  " : "BAD ")} {what}");
        if (!passed)
        {
            Failures.Add(what);
        }
    }

    private static void Wait(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);
}
