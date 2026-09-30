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

                Check("our text is on the clipboard", clipboard.GetText() == "hello");
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
        }
        finally
        {
            if (originalClipboard is not null)
            {
                using var restore = clipboard.ReplaceWithText(originalClipboard);
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
