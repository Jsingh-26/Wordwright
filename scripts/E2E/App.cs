using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Automation;

namespace E2E;

/// <summary>Drives one Wordwright instance in end-to-end test mode.</summary>
internal sealed class WordwrightUnderTest
{
    private readonly string _exe;
    public string DataFolder { get; }
    public Process? Process { get; private set; }
    public HashSet<int> AllPids { get; } = [];

    public WordwrightUnderTest(string exe, string dataFolder)
    {
        _exe = exe;
        DataFolder = dataFolder;
        Directory.CreateDirectory(dataFolder);
    }

    public int Pid => Process?.Id ?? 0;

    private Process Launch()
    {
        var psi = new ProcessStartInfo(_exe) { UseShellExecute = false };
        psi.Environment["WORDWRIGHT_E2E_DATA"] = DataFolder;
        return System.Diagnostics.Process.Start(psi)!;
    }

    /// <summary>Starts the instance; returns milliseconds until its first window or tray is up.</summary>
    public long Start(bool expectWindow)
    {
        var startsBefore = CountStarts();
        var sw = Stopwatch.StartNew();
        Process = Launch();
        lock (AllPids) AllPids.Add(Process.Id);
        if (expectWindow)
        {
            Ui.Wait(() => Ui.TopWindows(Pid).FirstOrDefault(), 15000);
        }
        else
        {
            // Started when its log says so: the tray icon is created just before.
            Ui.WaitTrue(() => CountStarts() > startsBefore, 15000);
            Thread.Sleep(500);
        }
        return sw.ElapsedMilliseconds;
    }

    /// <summary>A second launch: the running instance opens its main window.</summary>
    public AutomationElement OpenMainWindow()
    {
        var existing = MainWindow();
        if (existing is not null) { Native.Front(Ui.Hwnd(existing)); return existing; }
        using (var second = Launch()) { second.WaitForExit(10000); }
        var w = Ui.Wait(MainWindow, 10000);
        Native.Front(Ui.Hwnd(w));
        return w;
    }

    public AutomationElement? MainWindow() =>
        Ui.TopWindows(Pid).FirstOrDefault(w => w.Current.Name == "Wordwright"
            && Ui.Find(w, "Snippets", ControlType.ListItem) is not null
            || w.Current.Name == "Wordwright" && Ui.Find(w, "Show my snippets") is null && Ui.Find(w, "Settings") is not null);

    public AutomationElement? WelcomeWindow() =>
        Ui.TopWindows(Pid).FirstOrDefault(w => Ui.Find(w, "Show my snippets") is not null);

    public void Navigate(string page)
    {
        var w = OpenMainWindow();
        var item = Ui.Wait(() => Ui.Find(w, page, ControlType.ListItem) ?? Ui.Find(w, page));
        // Settings and About sit at the foot of the pane: if the window runs past
        // the bottom of the screen, bring it back before clicking.
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var r = item.Current.BoundingRectangle;
        if (r.IsEmpty || r.Bottom > screen.Bottom - 4 || r.Top < screen.Top)
        {
            var hwnd = Ui.Hwnd(w);
            var scale = Native.GetDpiForWindow(hwnd) / 96.0;
            Native.MoveWindow(hwnd, screen.Left + 20, screen.Top + 20, (int)(900 * scale), Math.Min((int)(640 * scale), screen.Height - 40), true);
            Thread.Sleep(500);
        }
        Native.Front(Ui.Hwnd(w));
        Ui.ClickCenter(item);
        Thread.Sleep(700);
    }

    public void Stop()
    {
        if (Process is null) return;
        try
        {
            if (!Process.HasExited) { Process.Kill(); Process.WaitForExit(5000); }
        }
        catch (InvalidOperationException) { }
        Process = null;
        Thread.Sleep(500);
    }

    public JsonObject SnippetsJson() => ReadJson("snippets.json");

    public JsonObject SettingsJson() => ReadJson("settings.json");

    private JsonObject ReadJson(string name)
    {
        for (var i = 0; i < 20; i++)
        {
            try { return JsonNode.Parse(File.ReadAllText(Path.Combine(DataFolder, name)))!.AsObject(); }
            catch (IOException) { Thread.Sleep(100); }
        }
        throw new IOException(name + " stayed locked");
    }

    public void WriteSettings(Action<JsonObject> change)
    {
        var json = SettingsJson();
        change(json);
        File.WriteAllText(Path.Combine(DataFolder, "settings.json"), json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public JsonObject? Snippet(string trigger) =>
        SnippetsJson()["snippets"]!.AsArray().Select(n => n!.AsObject()).FirstOrDefault(s => (string?)s["trigger"] == trigger);

    private int CountStarts() => LogText().Split((char)10).Count(l => l.TrimEnd().EndsWith(" started"));

    public string LogText()
    {
        var dir = Path.Combine(DataFolder, "logs");
        if (!Directory.Exists(dir)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var f in Directory.GetFiles(dir))
        {
            try
            {
                using var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var r = new StreamReader(s);
                sb.Append(r.ReadToEnd());
            }
            catch (IOException) { }
        }
        return sb.ToString();
    }
}
