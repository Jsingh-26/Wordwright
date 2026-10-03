using System.Windows;
using Application = System.Windows.Application;
using TextBox = System.Windows.Controls.TextBox;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using Clipboard = System.Windows.Clipboard;
using System.Windows.Controls;
using System.Windows.Interop;

namespace E2E;

/// <summary>
/// The runner's own typing target, started as a child process (<c>E2E.exe --target KLID</c>):
/// a window with one text box, whose UI thread uses the given keyboard layout.
/// ActivateKeyboardLayout changes that thread only, so testing German or French
/// never touches the layout of any other app or the user's language list.
/// </summary>
internal static class Target
{
    public const string Title = "Wordwright E2E target";

    /// <summary><c>E2E.exe --clip-owner N</c>: puts N characters of text, CSV and
    /// HTML on the clipboard and keeps a message loop running, as Excel does
    /// after a copy, so Windows can ask the owner for its data at any time.</summary>
    public static int ClipOwner(int size, string? exact = null)
    {
        var app = new Application();
        var window = new Window { Width = 1, Height = 1, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -100, Top = -100 };
        window.Loaded += (_, _) =>
        {
            var row = "1234.56\tWidget\tNorth\t2026-10-03\r\n";
            var text = new System.Text.StringBuilder(size + row.Length);
            while (text.Length < size) text.Append(row);
            var t = exact ?? text.ToString();
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, t);
            data.SetData(DataFormats.CommaSeparatedValue, t.Replace((char)9, ','));
            data.SetData(DataFormats.Html, "<table><tr><td>" + t[..Math.Min(t.Length, 200_000)] + "</td></tr></table>");
            Clipboard.SetDataObject(data, copy: false);
            Console.WriteLine(t.Length);
        };
        app.Run(window);
        return 0;
    }

    public static int Run(string klid, bool slowPaste = false)
    {
        var app = new Application();
        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Margin = new Thickness(12),
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(box, "TargetBox");
        System.Windows.Automation.AutomationProperties.SetName(box, "Target");
        if (slowPaste)
        {
            // A slow app, as over Remote Desktop or on a busy PC: it reads the
            // clipboard 700 ms after Ctrl+V arrives.
            box.PreviewKeyDown += async (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.V && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    e.Handled = true;
                    await Task.Delay(700);
                    box.Paste();
                }
            };
        }

        var window = new Window { Title = Title + " " + klid, Width = 640, Height = 360, Content = box, Left = 60, Top = 60 };

        IntPtr loaded = IntPtr.Zero;
        var wasLoaded = false;
        window.Loaded += (_, _) =>
        {
            if (klid != "none")
            {
                var before = Native.Layouts();
                loaded = Native.LoadKeyboardLayout(klid, 0x80); // KLF_NOTELLSHELL
                wasLoaded = before.Contains(loaded);
                Native.ActivateKeyboardLayout(loaded, 0);
            }
            box.Focus();
        };
        window.Closed += (_, _) =>
        {
            if (loaded != IntPtr.Zero && !wasLoaded)
            {
                Native.UnloadKeyboardLayout(loaded);
            }
        };
        app.Run(window);
        return 0;
    }
}
