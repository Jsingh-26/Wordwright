using System.Drawing;
using System.Text.Json;
using System.Windows.Automation;

namespace E2E;

/// <summary>
/// <c>E2E.exe --screenshots [dir]</c>: Microsoft Store screenshots of the real build
/// (docs/STORE_LISTING.md → Images). A test-mode instance with example snippets
/// that belong to nobody; every image is at least 1366×768 as Partner Center
/// requires, and a window smaller than that sits centred on a plain Steel panel
/// (the palette's light neutral) so nothing else on the screen shows.
/// </summary>
internal static class Screens
{
    private const int MinWidth = 1366, MinHeight = 768;
    private static readonly Color Steel = Color.FromArgb(0xE9, 0xEC, 0xF3);

    public static int Run(string exe, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var data = Path.Combine(Path.GetTempPath(), "wordwright-store-shots-" + DateTime.Now.ToString("HHmmss"));
        if (Directory.Exists(data)) Directory.Delete(data, true);
        var ww = new WordwrightUnderTest(exe, data);
        File.WriteAllText(Path.Combine(data, "settings.json"), """{ "theme": "light", "startWithWindows": false }""");

        // 1. Welcome, the moment ;date has just expanded in the try-it box.
        ww.Start(expectWindow: true);
        var welcome = Ui.Wait(ww.WelcomeWindow, 10000);
        Native.Front(Ui.Hwnd(welcome));
        var box = Ui.FindAll(welcome, ControlType.Edit).First();
        Ui.ClickCenter(box);
        var hkl = Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(Ui.Hwnd(welcome), out _));
        Native.Type(";date", hkl);
        Thread.Sleep(1500);
        Native.SetCursorPos(5, 5);
        Thread.Sleep(400);
        Save(Ui.Hwnd(welcome), outDir, "01-welcome-try-it");
        Ui.Invoke(Ui.Find(welcome, "Show my snippets")!);
        Thread.Sleep(800);
        ww.Stop();

        // 2. The main window with a realistic library.
        File.WriteAllText(Path.Combine(data, "snippets.json"), Library());
        ww.Start(expectWindow: false);
        var w = ww.OpenMainWindow();
        var hwnd = Ui.Hwnd(w);
        var scale = Native.GetDpiForWindow(hwnd) / 96.0;
        Native.ShowWindow(hwnd, 9);
        // Big enough for the Store's 1366×768 floor at any scaling of 100 % or more.
        var lw = Math.Max(1000, (int)Math.Ceiling(MinWidth / scale) + 2);
        var lh = Math.Max(640, (int)Math.Ceiling(MinHeight / scale) + 2);
        Native.MoveWindow(hwnd, (int)(30 * scale), (int)(20 * scale), (int)(lw * scale), (int)(lh * scale), true);
        Thread.Sleep(800);

        ww.Navigate("Snippets");
        SelectRow(w, "Office address");
        Save(hwnd, outDir, "02-snippets-editor");
        ww.Navigate("Settings");
        Save(hwnd, outDir, "03-settings");
        ww.Navigate("About");
        Save(hwnd, outDir, "04-about");

        // Dark: set through the settings file and restart, rather than through the
        // combo box, whose popup items UI Automation finds only slowly.
        var placement = new Native.Rect();
        Native.GetWindowRect(hwnd, out placement);
        ww.Stop();
        var settings = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(data, "settings.json")))!.AsObject();
        settings["theme"] = "dark";
        File.WriteAllText(Path.Combine(data, "settings.json"), settings.ToJsonString());
        ww.Start(expectWindow: false);
        w = ww.OpenMainWindow();
        hwnd = Ui.Hwnd(w);
        Native.MoveWindow(hwnd, placement.L, placement.T, placement.W, placement.H, true);
        Thread.Sleep(1200);
        ww.Navigate("Snippets");
        SelectRow(w, "Email signature");
        Save(hwnd, outDir, "05-snippets-dark");

        ww.Stop();
        try { Directory.Delete(data, true); } catch { }
        Console.WriteLine("screenshots: " + outDir);
        return 0;
    }

    private static void SelectRow(AutomationElement w, string name)
    {
        var row = Ui.Wait(() => Ui.FindAll(w, ControlType.ListItem).FirstOrDefault(r => r.Current.Name == name));
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Native.SetCursorPos(5, 5);
        Thread.Sleep(900);
    }

    /// <summary>The window as it is on screen, padded to the Store minimum when smaller.</summary>
    private static void Save(IntPtr hwnd, string dir, string name)
    {
        Native.Front(hwnd);
        Thread.Sleep(500);
        var r = Native.VisibleFrame(hwnd);
        // The visible frame: Windows 11 draws a 1 px border inside the rect and
        // an invisible resize margin around it on some windows; trim nothing, keep the shadow out.
        using var shot = Native.Capture(r);
        var width = Math.Max(MinWidth, shot.Width + 96);
        var height = Math.Max(MinHeight, shot.Height + 96);
        if (shot.Width >= MinWidth && shot.Height >= MinHeight)
        {
            width = shot.Width;
            height = shot.Height;
        }

        using var canvas = new Bitmap(width, height);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Steel);
            g.DrawImage(shot, (width - shot.Width) / 2, (height - shot.Height) / 2, shot.Width, shot.Height);
        }

        canvas.Save(Path.Combine(dir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"  {name}.png {width}×{height}");
    }

    /// <summary>Example snippets for the screenshots: everyday office text, names
    /// and places that are made up.</summary>
    private static string Library()
    {
        var now = DateTimeOffset.UtcNow;
        object S(string id, string trigger, string name, string body) => new
        {
            id, trigger, name, body, enabled = true, createdUtc = now, updatedUtc = now,
        };
        var doc = new
        {
            schemaVersion = 1,
            triggerPrefix = ";",
            snippets = new[]
            {
                S("a1b2c3d4", "date", "Today's date", "{date}"),
                S("b2c3d4e5", "sig", "Email signature", "Best regards,\nPriya Raman\nCustomer Success, Northwind Traders"),
                S("c3d4e5f6", "mtg", "Meeting follow-up", "Hi {cursor},\n\nThanks for your time today. As promised, here is a summary of what we agreed and the next steps.\n\nI'll send the updated proposal by Friday."),
                S("d4e5f6a7", "addr", "Office address", "Northwind Traders\n14 Market Street\nPune 411001"),
                S("e5f6a7b8", "thx", "Quick thanks", "Thanks for getting back to me so quickly."),
                S("f6a7b8c9", "ooo", "Out of office", "I'm away until Monday with limited access to email. For anything urgent, please contact support@northwind.example."),
                S("a7b8c9d0", "eod", "Reply by end of day", "Thanks, I'll look into this and get back to you by end of day."),
            },
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
