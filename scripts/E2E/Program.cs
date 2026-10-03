using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Automation;
using Axe.Windows.Automation;
using Axe.Windows.Automation.Data;

namespace E2E;

/// <summary>
/// End-to-end runner for Wordwright (docs/E2E.md). Each check carries the ID of
/// its row in docs/RELEASE_CHECKLIST.md. Usage:
///   dotnet run --project scripts/E2E -c Release [-- --exe path\to\Wordwright.App.exe] [--only E1,K1]
/// </summary>
internal static class Program
{
    private sealed record Result(string Id, string Check, string Outcome, string Detail);

    private static readonly List<Result> Results = [];
    private static string Out = "";
    private static WordwrightUnderTest Ww = null!;
    private static HashSet<string>? Only;
    private static readonly List<Process> Spawned = [];
    private static readonly List<IntPtr> RunnerLayouts = [];
    private static readonly HashSet<int> OfficePids = [];
    private static string Today => DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
    private const string SigBody = "Best regards,";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--target") return Target.Run(args[1], args.Contains("--slow-paste"));
        if (args.Length >= 2 && args[0] == "--clip-owner") return Target.ClipOwner(int.Parse(args[1]));
        if (args.Length >= 2 && args[0] == "--clip-owner-text") return Target.ClipOwner(0, Encoding.UTF8.GetString(Convert.FromBase64String(args[1])));

        Native.MakeDpiAware();
        var root = FindRepoRoot();
        var exe = Arg(args, "--exe") ?? Path.Combine(root, @"src\Wordwright.App\bin\Release\net10.0-windows10.0.19041.0\Wordwright.App.exe");
        Only = Arg(args, "--only")?.Split(',').ToHashSet(StringComparer.OrdinalIgnoreCase);
        Out = Path.Combine(root, "scripts", "e2e-results", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(Out);
        Ww = new WordwrightUnderTest(exe, Path.Combine(Out, "data"));

        Console.WriteLine($"Wordwright E2E  exe={exe}");
        Console.WriteLine($"results: {Out}");

        Native.ForegroundGuard = ForegroundIsOurs;
        var savedClipboard = Clip.GetText();
        var capsWasOn = (Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0;
        var net = new NetWatch();
        net.Start(Ww.AllPids);

        try
        {
            FirstRun();
            Window();
            Editor();
            Accessibility();
            Looks();
            TrayMenu();
            Expansion();
            Layouts();
            RealApps();
            Restarts();
        }
        catch (Exception ex)
        {
            Add("--", "runner", "Fail", "stopped early: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            net.Stop();
            Check("R2", "No network connections at any time", () =>
                net.Seen.Count == 0 ? Pass($"{net.Samples} samples of the TCP/UDP tables, none owned by Wordwright")
                                    : Fail(string.Join("; ", net.Seen)));
            Check("R3", "The log holds event names only", LogHasNoContent);
            Cleanup(savedClipboard, capsWasOn);
            WriteReport(exe);
        }

        return Results.Any(r => r.Outcome == "Fail") ? 1 : 0;
    }

    // ---------------------------------------------------------------- phases

    private static void FirstRun()
    {
        long ms = 0;
        Check("R4", "Start-up: launch to first window in under 1 s", () =>
        {
            ms = Ww.Start(expectWindow: true);
            return ms < 1000 ? Pass($"{ms} ms to the welcome window (cold start of a Release build)") : Fail($"{ms} ms");
        });
        if (Ww.Process is null) Ww.Start(expectWindow: true);

        Check("I1a", "Fresh data folder: welcome window and the three example snippets", () =>
        {
            var w = Ui.Wait(Ww.WelcomeWindow, 8000);
            Shot(Ui.Hwnd(w), "welcome");
            var triggers = Ww.SnippetsJson()["snippets"]!.AsArray().Select(n => (string?)n!["trigger"]).ToList();
            return triggers.SequenceEqual(["date", "thanks", "sig"]) ? Pass("welcome shown; seeded " + string.Join(", ", triggers)) : Fail("seeded: " + string.Join(", ", triggers));
        });

        Check("E5", "Welcome window: ;date in the try-it box expands in place, the label switches to the done text", () =>
        {
            var w = Ui.Wait(Ww.WelcomeWindow);
            var box = Ui.FindAll(w, ControlType.Edit).First();
            Ui.ClickCenter(box);
            Native.Type(";date", Layout());
            var ok = Ui.WaitTrue(() => Ui.Text(box) == Today, 3000);
            var done = Ui.Find(w, "That works everywhere: Word, Outlook, your browser, any app.") is not null;
            Shot(Ui.Hwnd(w), "welcome-done");
            return ok && done ? Pass($"box reads \"{Ui.Text(box)}\"; done text shown") : Fail($"box \"{Ui.Text(box)}\", done text {(done ? "shown" : "missing")}");
        });

        Check("A2a", "Welcome window: Axe.Windows finds no accessibility errors", () => Axe("welcome"));

        var welcome = Ww.WelcomeWindow();
        if (welcome is not null) Ui.Invoke(Ui.Find(welcome, "Show my snippets")!);
        Thread.Sleep(1000);
    }

    private static void Window()
    {
        Check("S2a", "All pages captured at the current size, 800×600 and maximised", () =>
        {
            var w = Ww.OpenMainWindow();
            var hwnd = Ui.Hwnd(w);
            foreach (var page in new[] { "Snippets", "Settings", "About" }) { Ww.Navigate(page); Shot(hwnd, "page-" + page); }
            var scale = Native.GetDpiForWindow(hwnd) / 96.0;
            Native.GetWindowRect(hwnd, out var r);
            Native.MoveWindow(hwnd, r.L, r.T, (int)(800 * scale), (int)(600 * scale), true); Thread.Sleep(600);
            foreach (var page in new[] { "Snippets", "Settings", "About" }) { Ww.Navigate(page); Shot(hwnd, "page-" + page + "-800x600"); }
            Native.ShowWindow(hwnd, 3); Thread.Sleep(800);
            foreach (var page in new[] { "Snippets", "Settings", "About" }) { Ww.Navigate(page); Shot(hwnd, "page-" + page + "-max"); }
            Native.ShowWindow(hwnd, 9); Thread.Sleep(600);
            return Pass($"9 captures at {Native.GetDpiForWindow(hwnd) * 100 / 96} % scaling");
        });

        Check("S5", "Resize below 800×600 is refused; close and reopen, the window is where it was", () =>
        {
            var w = Ww.OpenMainWindow();
            var hwnd = Ui.Hwnd(w);
            var scale = Native.GetDpiForWindow(hwnd) / 96.0;
            Native.MoveWindow(hwnd, 120, 90, (int)(500 * scale), (int)(380 * scale), true); Thread.Sleep(500);
            Native.GetWindowRect(hwnd, out var small);
            var lw = small.W / scale; var lh = small.H / scale;
            Native.MoveWindow(hwnd, (int)(40 * scale), (int)(30 * scale), (int)(900 * scale), (int)(620 * scale), true); Thread.Sleep(500);
            Native.GetWindowRect(hwnd, out var before);
            Ui.Invoke(Ui.Find(w, "Close", ControlType.Button)!);
            Ui.WaitTrue(() => Ww.MainWindow() is null || Ww.MainWindow()!.Current.IsOffscreen, 3000);
            var again = Ww.OpenMainWindow();
            Native.GetWindowRect(Ui.Hwnd(again), out var after);
            var refused = lw >= 799 && lh >= 599;
            var same = Math.Abs(after.L - before.L) <= 2 && Math.Abs(after.T - before.T) <= 2 && Math.Abs(after.W - before.W) <= 2 && Math.Abs(after.H - before.H) <= 2;
            return refused && same ? Pass($"asked for 500×380, got {lw:0}×{lh:0}; reopened at {after} (was {before})")
                                   : Fail($"asked for 500×380, got {lw:0}×{lh:0}; before {before}, after {after}");
        });

        Check("S6", "Hovering Maximise shows the Snap Layouts flyout", () =>
        {
            var w = Ww.OpenMainWindow();
            var max = Ui.Find(w, "Maximize", ControlType.Button) ?? Ui.Find(w, "Maximise", ControlType.Button);
            if (max is null) return Fail("no Maximize button found");
            var r = Ui.RectOf(max);
            // The flyout is drawn by the shell and is not in the automation tree, so
            // compare the area under the button before and during the hover.
            var below = new Native.Rect { L = r.L - 260, T = r.B + 10, R = r.R + 10, B = r.B + 330 };
            var still = Native.Capture(below);
            Native.SetCursorPos(r.L + r.W / 2 - 3, r.T + r.H / 2); Thread.Sleep(150);
            Native.SetCursorPos(r.L + r.W / 2, r.T + r.H / 2);
            double diff = 0;
            Ui.WaitTrue(() => (diff = Difference(still, Native.Capture(below))) > 12, 3000, 200);
            Native.Save(Native.Capture(new Native.Rect { L = r.L - 700, T = r.T - 10, R = r.R + 20, B = r.B + 420 }), Out, "snap-hover");
            still.Dispose();
            Native.SetCursorPos(10, 10); Thread.Sleep(400);
            return diff > 12 ? Pass($"flyout drawn under the button (mean pixel change {diff:0.0}); see snap-hover.png")
                             : Fail($"no change under the button (mean pixel change {diff:0.0}); see snap-hover.png");
        });
    }

    private static void Editor()
    {
        var w = Ww.OpenMainWindow();
        Ww.Navigate("Snippets");

        Check("I3a", "Editor: a 10,000-character snippet typed, then at once another snippet selected; nothing lost", () =>
        {
            var before = Rows(w).Length;
            Ui.Invoke(Ui.Find(w, "New snippet", ControlType.Button)!);
            Ui.WaitTrue(() => Rows(w).Length == before + 1, 3000);
            Ui.SetValue(Field(w, "Name"), "E2E long");
            Ui.SetValue(Field(w, "Shortcut"), "long");
            Ui.SetValue(Field(w, "Text"), LongBody);
            Select(Rows(w)[0]);
            var ok = Ui.WaitTrue(() => (string?)Ww.Snippet("long")?["body"] == LongBody, 3000);
            return ok ? Pass("saved on switch: trigger \"long\", body 10,000 characters") : Fail("not saved after switching snippet");
        });

        Check("I3b", "Editor: an edit, then at once a page change; nothing lost", () =>
        {
            Select(Rows(w).First(r => r.Current.Name.Contains("Thanks")));
            Ui.SetValue(Field(w, "Name"), "Thanks E2E");
            Ww.Navigate("Settings");
            var ok = Ui.WaitTrue(() => (string?)Ww.Snippet("thanks")?["name"] == "Thanks E2E", 3000);
            Ww.Navigate("Snippets");
            return ok ? Pass("name saved when leaving the page") : Fail("name not saved after page change");
        });

        Check("I1b", "Create and delete through the window; the list and the file agree", () =>
        {
            var before = Rows(w).Length;
            Ui.Invoke(Ui.Find(w, "New snippet", ControlType.Button)!);
            Ui.WaitTrue(() => Rows(w).Length == before + 1, 3000);
            Ui.SetValue(Field(w, "Name"), "AltGr");
            Ui.SetValue(Field(w, "Shortcut"), "eur");
            Ui.SetValue(Field(w, "Text"), AltGrBody);
            Select(Rows(w)[0]);
            Ui.WaitTrue(() => Ww.Snippet("eur") is not null, 3000);

            Ui.Invoke(Ui.Find(w, "New snippet", ControlType.Button)!);
            Ui.WaitTrue(() => Rows(w).Length == before + 2, 3000);
            Ui.SetValue(Field(w, "Name"), "Caps Lock");
            Ui.SetValue(Field(w, "Shortcut"), "CAPS");
            Ui.SetValue(Field(w, "Text"), "caps ok");
            Select(Rows(w)[0]);
            Ui.WaitTrue(() => Ww.Snippet("CAPS") is not null, 3000);
            before++;

            Ui.Invoke(Ui.Find(w, "New snippet", ControlType.Button)!);
            Ui.WaitTrue(() => Rows(w).Length == before + 2, 3000);
            Ui.SetValue(Field(w, "Name"), "To delete");
            Ui.SetValue(Field(w, "Shortcut"), "gone");
            Thread.Sleep(900);
            Ui.Invoke(Ui.Find(w, "Delete snippet", ControlType.Button)!);
            var confirm = Ui.Wait(() => Ui.FindAll(w, ControlType.Button).Where(b => b.Current.Name == "Delete snippet").Skip(1).FirstOrDefault(), 3000);
            Shot(Ui.Hwnd(w), "delete-confirm");
            Ui.Invoke(confirm);
            var gone = Ui.WaitTrue(() => Rows(w).Length == before + 1 && Ww.Snippet("gone") is null, 3000);
            return gone && Ww.Snippet("eur") is not null && Ww.Snippet("CAPS") is not null ? Pass("created \"eur\" and \"CAPS\", created and deleted \"gone\"; snippets.json matches the list")
                                                        : Fail($"rows {Rows(w).Length} (expected {before + 1}); gone {(Ww.Snippet("gone") is null ? "deleted" : "still there")}");
        });
    }

    private static void Accessibility()
    {
        Check("A3", "Every list row and field has a spoken name (what Narrator reads)", () =>
        {
            var w = Ww.OpenMainWindow();
            Ww.Navigate("Snippets");
            var names = Rows(w).Select(r => r.Current.Name).ToList();
            var bad = names.Where(n => string.IsNullOrWhiteSpace(n) || n.StartsWith("Wordwright.", StringComparison.Ordinal)).ToList();
            var fields = new[] { "Name", "Shortcut", "Text", "Search snippets" }.Where(n => Ui.Find(w, n, ControlType.Edit) is null).ToList();
            return bad.Count == 0 && fields.Count == 0 ? Pass("rows read as: " + string.Join(" | ", names))
                                                      : Fail($"unnamed rows: {bad.Count} ({string.Join(" | ", names)}); fields without names: {string.Join(", ", fields)}");
        });

        Check("A1", "Keyboard only: Ctrl+F, Ctrl+N, Delete, Tab through the page with focus visible", () =>
        {
            var w = Ww.OpenMainWindow();
            Ww.Navigate("Snippets");
            Select(Rows(w)[0]);
            var notes = new List<string>();
            Native.Chord(Layout(), Native.VK_CONTROL, 0x46); Thread.Sleep(300);
            var f = Ui.FocusedName(); notes.Add("Ctrl+F → " + f);
            var searchOk = f.Contains("Search snippets");
            var before = Rows(w).Length;
            Native.Chord(Layout(), Native.VK_CONTROL, 0x4E); Thread.Sleep(600);
            var newOk = Rows(w).Length == before + 1; notes.Add($"Ctrl+N → {Rows(w).Length - before} new row");
            Ui.Invoke(Ui.Find(w, "Delete snippet", ControlType.Button)!);
            var confirm = Ui.Wait(() => Ui.FindAll(w, ControlType.Button).Where(b => b.Current.Name == "Delete snippet").Skip(1).FirstOrDefault(), 3000);
            Ui.Invoke(confirm); Thread.Sleep(500);
            Select(Rows(w)[0]); Thread.Sleep(200);
            Native.Tap(Native.VK_DELETE); Thread.Sleep(600);
            var dialog = Ui.FindAll(w, ControlType.Button).Count(b => b.Current.Name == "Cancel") > 0;
            notes.Add("Delete in the list → " + (dialog ? "confirmation" : "nothing"));
            Native.Tap(Native.VK_ESCAPE); Thread.Sleep(400);
            var cancelled = Rows(w).Length == before;
            Native.Chord(Layout(), Native.VK_CONTROL, 0x46); Thread.Sleep(200);
            var stops = new List<string>();
            for (var i = 0; i < 12; i++)
            {
                Native.Tap(Native.VK_TAB); Thread.Sleep(150);
                stops.Add(Ui.FocusedName());
                if (i is 2 or 5 or 8) Shot(Ui.Hwnd(w), $"focus-tab{i + 1}");
            }
            notes.Add("Tab stops: " + string.Join(" → ", stops));
            var unnamedStops = stops.Count(s => s.EndsWith("\"\""));
            return searchOk && newOk && dialog && cancelled && unnamedStops == 0
                ? Pass(string.Join("; ", notes))
                : Fail(string.Join("; ", notes) + $"; Esc kept the row: {cancelled}; unnamed stops: {unnamedStops}");
        });

        Check("A2", "Axe.Windows (Accessibility Insights engine): no errors on Snippets, Settings, About", () =>
        {
            var details = new List<string>();
            var errors = 0;
            foreach (var page in new[] { "Snippets", "Settings", "About" })
            {
                Ww.Navigate(page);
                if (page == "Snippets")
                {
                    // Focus in a field that holds text, so the controls that only
                    // appear then (the clear button) are scanned too.
                    var w = Ww.OpenMainWindow();
                    Select(Rows(w)[0]);
                    Ui.ClickCenter(Field(w, "Name"));
                    Thread.Sleep(400);
                }
                var (n, d) = AxeScan(page);
                errors += n; details.Add($"{page}: {n}{(d.Length > 0 ? " (" + d + ")" : "")}");
            }
            return errors == 0 ? Pass(string.Join("; ", details)) : Fail(string.Join("; ", details));
        });
    }

    private static void Looks()
    {
        Check("S8", "Theme setting re-skins the window at once (Light, Dark, System) and is saved", () =>
        {
            var w = Ww.OpenMainWindow();
            var notes = new List<string>();
            foreach (var choice in new[] { "Light", "Dark", "System" })
            {
                Ww.Navigate("Settings");
                SetTheme(w, choice);
                var saved = Ui.WaitTrue(() => (string?)Ww.SettingsJson()["theme"] == choice.ToLowerInvariant(), 3000);
                foreach (var page in new[] { "Snippets", "Settings" }) { Ww.Navigate(page); Shot(Ui.Hwnd(w), $"theme-{choice}-{page}"); }
                var lum = Luminance(Ui.Hwnd(w));
                notes.Add($"{choice}: saved {saved}, window luminance {lum}");
            }
            var light = notes[0].Contains("saved True") && Lum(notes[0]) > 150;
            var dark = notes[1].Contains("saved True") && Lum(notes[1]) < 100;
            return light && dark && notes[2].Contains("saved True") ? Pass(string.Join("; ", notes)) : Fail(string.Join("; ", notes));
        });
    }

    private static void TrayMenu()
    {
        Check("S7", "Tray menu opens from the tray icon, in the taskbar theme, on Acrylic with rounded corners", () =>
        {
            var items = Tray.OpenMenu(Ww.Pid);
            if (items is null) return Fail("tray icon or menu not found");
            var menuWindow = Ui.TopWindows(Ww.Pid).First(t => Ui.FindAll(t, ControlType.MenuItem).Length > 0);
            var r = Ui.RectOf(menuWindow);
            Native.Save(Native.Capture(new Native.Rect { L = r.L - 24, T = r.T - 24, R = r.R + 24, B = r.B + 24 }), Out, "tray-menu");
            var names = items.Select(i => i.Current.Name).ToList();
            Native.Tap(Native.VK_ESCAPE); Thread.Sleep(300);
            Tray.CloseOverflow();
            return names.SequenceEqual(["Open Wordwright", "Snippets on", "Quit Wordwright"]) ? Pass("items: " + string.Join(" | ", names) + "; look: tray-menu.png")
                                                                                          : Fail("items: " + string.Join(" | ", names));
        });

        Check("S7a", "Tray glyph dims when Snippets is switched off, and comes back when switched on", () =>
        {
            var lightTaskbar = TaskbarIsLight();
            var (on, _) = Tray.MeasureGlyph(Out, "tray-icon-on"); Tray.CloseOverflow();
            if (on < 0) return Fail("tray icon not found");
            if (!ToggleSnippets()) return Fail("could not click Snippets on");
            Thread.Sleep(800);
            var (off, _) = Tray.MeasureGlyph(Out, "tray-icon-off"); Tray.CloseOverflow();
            var saved = (bool?)Ww.SettingsJson()["snippetsEnabled"] == false;
            ToggleSnippets(); Thread.Sleep(800);
            var (back, _) = Tray.MeasureGlyph(Out, "tray-icon-back"); Tray.CloseOverflow();
            // The first reading can catch the icon's hover highlight, so the full
            // strength is the brighter (dark taskbar) of the two "on" readings.
            var full = lightTaskbar ? Math.Min(on, back) : Math.Max(on, back);
            var dimmed = lightTaskbar ? off > full + 40 : off < full * 0.75;
            var restored = lightTaskbar ? back < off - 30 : back > off * 1.3;
            var how = lightTaskbar ? "darkest" : "brightest";
            return dimmed && restored && saved ? Pass($"{how} glyph pixel: on {on}, off {off}, on again {back} (dark taskbar: {!lightTaskbar})")
                                               : Fail($"{how} glyph pixel: on {on}, off {off}, on again {back}; setting saved off: {saved}");
        });
    }

    /// <summary>Sends the main window to the tray so it cannot cover the apps the
    /// typing tests use.</summary>
    private static void HideMainWindow()
    {
        var w = Ww.MainWindow();
        if (w is null) return;
        try { ((WindowPattern)w.GetCurrentPattern(WindowPattern.Pattern)).Close(); } catch { }
        Thread.Sleep(500);
    }

    private static void Expansion()
    {
        HideMainWindow();
        var target = StartTarget("none");

        Check("E2", "Clipboard is the same afterwards, and nothing from Wordwright is in Win+V history", () =>
        {
            var marker = "E2E clipboard marker " + Guid.NewGuid().ToString("N")[..8];
            var owner = OwnClipboard(marker);
            TypeInto(target, ";sig");
            var expanded = Ui.WaitTrue(() => TargetText(target).StartsWith(SigBody), 3000);
            Thread.Sleep(1200);
            var clip = Clip.GetText();
            var history = Clip.HistoryContains(SigBody);
            try { owner.Kill(); } catch { }
            return expanded && clip == marker && history == false
                ? Pass($"expanded; clipboard back to the marker; Win+V history ({Clip.HistoryNote}) has no snippet text")
                : Fail($"expanded {expanded}, box holds \"{Short(TargetText(target))}\", foreground \"{ForegroundName()}\"; clipboard {(clip == marker ? "restored" : "holds \"" + Short(clip ?? "(nothing readable)") + "\"")}; history has snippet: {history?.ToString() ?? Clip.HistoryNote}");
        });

        Check("E3", "A 10,000-character snippet inserts in under 1 s", () =>
        {
            ClearTarget(target);
            TypeInto(target, ";long");
            var sw = Stopwatch.StartNew();
            var ok = Ui.WaitTrue(() => TargetText(target).Length >= LongBody.Length, 5000, 10);
            var ms = sw.ElapsedMilliseconds;
            return ok && ms < 1000 ? Pass($"{ms} ms from the last key to all 10,000 characters in the box") : Fail(ok ? $"{ms} ms" : "never arrived");
        });

        Check("E4", "Typing delay: ;si, six seconds, g → nothing; ;sig at normal speed → expands", () =>
        {
            ClearTarget(target);
            TypeInto(target, ";si");
            Thread.Sleep(6000);
            Native.Type("g", Layout(target));
            Thread.Sleep(1500);
            var slow = TargetText(target);
            ClearTarget(target);
            TypeInto(target, ";sig");
            var fast = Ui.WaitTrue(() => TargetText(target).StartsWith(SigBody), 3000);
            return slow == ";sig" && fast ? Pass("slow: left as \";sig\"; normal speed: expanded") : Fail($"slow left \"{slow}\"; normal expanded {fast}");
        });

        Check("E6", "Heavy clipboard (1 MB and 8 MB of text, HTML and CSV, held by a live owner as Excel holds a copy): expansion under 1 s, then the clipboard comes back whole", () =>
        {
            var notes = new List<string>();
            var all = true;
            foreach (var size in new[] { 1_000_000, 8_000_000 })
            {
                var owner = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--clip-owner {size}") { UseShellExecute = false, RedirectStandardOutput = true })!;
                Spawned.Add(owner);
                var big = int.Parse(owner.StandardOutput.ReadLine() ?? "0");
                Thread.Sleep(300);
                ClearTarget(target);
                TypeInto(target, ";sig");
                var sw = Stopwatch.StartNew();
                var ok = Ui.WaitTrue(() => TargetText(target).StartsWith(SigBody), 30000, 10);
                var ms = sw.ElapsedMilliseconds;
                Thread.Sleep(1500);
                var back = Clip.GetText();
                var whole = back?.Length == big;
                notes.Add($"{size / 1_000_000} MB: {(ok ? ms + " ms" : "no expansion in 30 s")}, clipboard {(whole ? "back whole" : (back?.Length ?? 0).ToString("N0") + " of " + big.ToString("N0"))}");
                all &= ok && ms < 1000 && whole;
                try { owner.Kill(); } catch { }
            }
            notes.Add("Excel: " + ExcelCopy(target));
            return all && notes[^1].StartsWith("Excel: ok") ? Pass(string.Join("; ", notes)) : Fail(string.Join("; ", notes));
        });

        Check("E8", "Excluded app: no expansion in an app on the excluded list", () =>
        {
            Ww.Stop();
            Ww.WriteSettings(s => s["excludedApps"]!.AsArray().Add("E2E.exe"));
            Ww.Start(expectWindow: false);
            ClearTarget(target);
            TypeInto(target, ";sig");
            Thread.Sleep(1500);
            var excluded = TargetText(target);
            Ww.Stop();
            Ww.WriteSettings(s =>
            {
                var list = s["excludedApps"]!.AsArray();
                var mine = list.FirstOrDefault(n => (string?)n == "E2E.exe");
                if (mine is not null) list.Remove(mine);
            });
            Ww.Start(expectWindow: false);
            ClearTarget(target);
            TypeInto(target, ";sig");
            var normal = Ui.WaitTrue(() => TargetText(target).StartsWith(SigBody), 3000);
            return excluded == ";sig" && normal ? Pass("excluded: left as typed; removed from the list: expands again") : Fail($"excluded left \"{excluded}\"; after removal expanded {normal}");
        });

        StopTarget(target);

        Check("E7", "A slow app (reads the clipboard 700 ms after Ctrl+V, as over Remote Desktop): the snippet arrives, not the old clipboard", () =>
        {
            var slow = StartTarget("none", slowPaste: true);
            var marker = "E2E old clipboard " + Guid.NewGuid().ToString("N")[..8];
            var owner = OwnClipboard(marker);
            TypeInto(slow, ";sig");
            Ui.WaitTrue(() => TargetText(slow).Length > 3, 5000);
            Thread.Sleep(1500);
            var text = TargetText(slow);
            // When a background app reads the text first, the restore waits for its 3 s deadline.
            Ui.WaitTrue(() => Clip.GetText() == marker, 4000, 200);
            var clip = Clip.GetText();
            try { owner.Kill(); } catch { }
            StopTarget(slow);
            return text.StartsWith(SigBody) && clip == marker
                ? Pass("the snippet arrived; the old clipboard came back afterwards")
                : Fail($"box holds \"{Short(text)}\"; clipboard {(clip == marker ? "restored" : "holds \"" + Short(clip ?? "") + "\"")}");
        });
    }

    private static void Layouts()
    {
        var layouts = new (string klid, string name)[] { ("00000407", "German"), ("0000040A", "Spanish"), ("0000040C", "French"), ("00000809", "UK") };

        Check("K1", "German, Spanish, French and UK layouts: ;date with the default prefix", () =>
        {
            var notes = new List<string>();
            var all = true;
            foreach (var (klid, name) in layouts)
            {
                var t = StartTarget(klid);
                var hkl = Layout(t);
                var keys = Describe(";", hkl);
                TypeInto(t, ";date");
                var ok = Ui.WaitTrue(() => TargetText(t) == Today, 3000);
                notes.Add($"{name} (\";\" is {keys}): {(ok ? "expanded" : "left \"" + TargetText(t) + "\"")}");
                all &= ok;
                StopTarget(t);
            }
            return all ? Pass(string.Join("; ", notes)) : Fail(string.Join("; ", notes));
        });

        Check("K2", "Same layouts with a Shift prefix (:), with Caps Lock on, and an AltGr snippet body", () =>
        {
            SetPrefix(":");
            var notes = new List<string>();
            var all = true;
            foreach (var (klid, name) in layouts)
            {
                var t = StartTarget(klid);
                var hkl = Layout(t);
                TypeInto(t, ":date");
                var shiftOk = Ui.WaitTrue(() => TargetText(t) == Today, 3000);

                // Caps Lock: the prefix first, then Caps Lock on and the letters, so
                // the screen shows ":CAPS"; the snippet "CAPS" must match what the
                // screen shows, which proves the hook reads Caps Lock as Windows does.
                ClearTarget(t);
                TypeInto(t, ":");
                Native.Tap(Native.VK_CAPITAL); Thread.Sleep(100);
                Native.Type("caps", hkl);
                var capsOk = Ui.WaitTrue(() => TargetText(t) == "caps ok", 3000);
                var capsText = TargetText(t);
                Native.Tap(Native.VK_CAPITAL); Thread.Sleep(100);

                ClearTarget(t);
                TypeInto(t, ":eur");
                var altOk = Ui.WaitTrue(() => TargetText(t) == AltGrBody, 3000);
                notes.Add($"{name}: \":\" {(shiftOk ? "ok" : "no")}, Caps Lock {(capsOk ? "ok (:CAPS)" : "left \"" + capsText + "\"")}, AltGr body {(altOk ? "ok" : "\"" + TargetText(t) + "\"")}");
                all &= shiftOk && capsOk && altOk;
                StopTarget(t);
            }
            SetPrefix(";");
            return all ? Pass(string.Join("; ", notes)) : Fail(string.Join("; ", notes));
        });
    }

    private static void RealApps()
    {
        HideMainWindow();
        Check("E1", "Expands in real apps: Notepad, Edge, Chrome, VS Code, Word, the Windows search box", () =>
        {
            var notes = new List<string>();
            var fails = 0;
            void App(string name, Func<string> run)
            {
                if (Only is not null && !Only.Contains("E1") && !Only.Contains("E1:" + name)) return;
                string r;
                try { r = run(); } catch (Exception ex) { r = "error: " + ex.Message; }
                if (!r.StartsWith("ok") && !r.StartsWith("n/a")) fails++;
                notes.Add(name + " " + r);
                Console.WriteLine("   " + name + " " + r);
            }

            App("Notepad", () => FileApp("notepad.exe", "notepad.txt", "Notepad", null));
            App("VS Code", () => FileApp(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
                "vscode.txt", "Visual Studio Code", d => $"--user-data-dir \"{d}\\vscode-profile\" --extensions-dir \"{d}\\vscode-ext\" --disable-workspace-trust --new-window"));
            App("Edge", () => BrowserApp(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "edge"));
            App("Chrome", () => BrowserApp(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome"));
            App("Word", WordApp);
            App("Windows search", SearchApp);
            notes.Add("Outlook, Teams, Slack n/a: they need a signed-in account; Teams and Slack are Chromium, which Edge and Chrome cover");
            return fails == 0 ? Pass(string.Join("; ", notes)) : Fail(string.Join("; ", notes));
        });
    }

    private static void Restarts()
    {
        Check("I3c", "Editor: an edit, then at once Quit from the tray; the edit is there after a restart", () =>
        {
            var w = Ww.OpenMainWindow();
            Ww.Navigate("Snippets");
            Select(Rows(w).First(r => r.Current.Name.Contains("Thanks")));
            Ui.SetValue(Field(w, "Name"), "Thanks quit");
            var items = Tray.OpenMenu(Ww.Pid);
            if (items is null) return Fail("tray menu not found");
            Ui.Invoke(items.First(i => i.Current.Name == "Quit Wordwright"));
            var exited = Ww.Process!.WaitForExit(8000);
            Tray.CloseOverflow();
            Ww.Start(expectWindow: false);
            var w2 = Ww.OpenMainWindow();
            Ww.Navigate("Snippets");
            var shown = Rows(w2).Any(r => r.Current.Name.Contains("Thanks quit"));
            return exited && shown && (string?)Ww.Snippet("thanks")?["name"] == "Thanks quit"
                ? Pass("quit from the tray exited cleanly; after restart the list shows \"Thanks quit\"")
                : Fail($"exited {exited}; shown after restart {shown}");
        });

        Check("S8b", "Theme survives a restart", () =>
        {
            var w = Ww.OpenMainWindow();
            Ww.Navigate("Settings");
            SetTheme(w, "Light");
            Ui.WaitTrue(() => (string?)Ww.SettingsJson()["theme"] == "light", 3000);
            Ww.Stop();
            Ww.Start(expectWindow: false);
            var w2 = Ww.OpenMainWindow();
            Thread.Sleep(800);
            var lum = Luminance(Ui.Hwnd(w2));
            Shot(Ui.Hwnd(w2), "theme-after-restart");
            Ww.Navigate("Settings");
            SetTheme(w2, "System");
            return lum > 150 ? Pass($"light after restart (window luminance {lum})") : Fail($"window luminance {lum} after restart");
        });
    }

    // ---------------------------------------------------------------- app targets

    private static string FileApp(string exe, string file, string titlePart, Func<string, string>? extraArgs)
    {
        if (!File.Exists(exe) && !exe.EndsWith("notepad.exe")) return "n/a: not installed";
        var path = Path.Combine(Out, "data", file);
        File.WriteAllText(path, "");
        var args = (extraArgs?.Invoke(Path.Combine(Out, "data")) ?? "") + $" \"{path}\"";
        var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true })!;
        Spawned.Add(p);
        var window = Ui.Wait(() => Ui.Root.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>()
            .FirstOrDefault(w => (w.Current.Name ?? "").Contains(file) && (w.Current.Name ?? "").Contains(titlePart)), 60000);
        Thread.Sleep(titlePart == "Notepad" ? 1000 : 6000);
        var editor = Ui.FindAll(window, ControlType.Document).FirstOrDefault() ?? Ui.FindAll(window, ControlType.Edit).FirstOrDefault();
        if (editor is not null) Ui.ClickCenter(editor);
        else { var r = Ui.RectOf(window); Native.Click(r.L + r.W / 2, r.T + r.H / 2); }
        Thread.Sleep(300);
        Native.Type(";sig", Layout());
        Thread.Sleep(1500);
        Native.Chord(Layout(), Native.VK_CONTROL, 0x53); // Ctrl+S: read the result back from the file
        Thread.Sleep(1500);
        var text = File.ReadAllText(path);
        Native.Chord(Layout(), Native.VK_CONTROL, titlePart == "Notepad" ? (ushort)0x57 : (ushort)0x57); // Ctrl+W closes this tab only
        Thread.Sleep(800);
        if (titlePart != "Notepad") { try { p.Kill(true); } catch { } }
        return text.Replace("\r\n", "\n").StartsWith("Best regards,\n") ? "ok" : $"file holds \"{Short(text)}\"";
    }

    private static string BrowserApp(string exe, string name)
    {
        if (!File.Exists(exe)) return "n/a: not installed";
        var profile = Path.Combine(Out, "data", name + "-profile");
        var page = "data:text/html,<title>e2e</title><textarea aria-label=e2e autofocus style='width:90%;height:300px'></textarea>";
        var p = Process.Start(new ProcessStartInfo(exe, $"--user-data-dir=\"{profile}\" --no-first-run --no-default-browser-check --app=\"{page}\"") { UseShellExecute = false })!;
        Spawned.Add(p);
        try
        {
            var window = Ui.Wait(() => Ui.Root.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "e2e")).Cast<AutomationElement>().FirstOrDefault(), 30000);
            var area = Ui.Wait(() => Ui.Find(window, "e2e", ControlType.Edit), 15000);
            Ui.ClickCenter(area);
            Native.Type(";sig", Layout());
            var ok = Ui.WaitTrue(() => Ui.Text(area).StartsWith(SigBody), 3000);
            return ok ? "ok" : $"textarea holds \"{Short(Ui.Text(area))}\"";
        }
        finally { try { p.Kill(true); } catch { } }
    }

    private static string WordApp()
    {
        var type = Type.GetTypeFromProgID("Word.Application");
        if (type is null) return "n/a: not installed";
        if (Process.GetProcessesByName("WINWORD").Length > 0) return "n/a: Word is already open; left alone";
        dynamic word = Activator.CreateInstance(type)!;
        var hwnd = IntPtr.Zero;
        try
        {
            word.Visible = true;
            dynamic doc = word.Documents.Add();
            doc.Activate();
            Thread.Sleep(3000);
            hwnd = (IntPtr)(int)word.ActiveWindow.Hwnd;
            Native.GetWindowThreadProcessId(hwnd, out var wordPid); OfficePids.Add((int)wordPid);
            word.Activate();
            Thread.Sleep(500);
            Native.Front(hwnd);
            var page = Ui.Wait(() => Ui.FindAll(AutomationElement.FromHandle(hwnd), ControlType.Document).FirstOrDefault(), 15000);
            Ui.ClickCenter(page);
            Thread.Sleep(500);
            Native.Type(";sig", Layout());
            Thread.Sleep(1500);
            ScreenShot("word-after-typing");
            var text = "";
            Ui.WaitTrue(() => (text = (string)doc.Content.Text).StartsWith(SigBody), 5000, 200);
            doc.Close(0);
            return text.StartsWith(SigBody) ? "ok" : $"document holds \"{Short(text)}\"";
        }
        finally { EndOffice(word, hwnd); }
    }

    /// <summary>The checklist's E6 as written: a real Excel range copied, ;sig
    /// typed, then the range must still be on the clipboard.</summary>
    private static string ExcelCopy(TargetWindow target)
    {
        var type = Type.GetTypeFromProgID("Excel.Application");
        if (type is null) return "n/a: not installed";
        if (Process.GetProcessesByName("EXCEL").Length > 0) return "n/a: Excel is already open; left alone";
        dynamic excel = Activator.CreateInstance(type)!;
        try
        {
            excel.DisplayAlerts = false;
            Native.GetWindowThreadProcessId((IntPtr)(int)excel.Hwnd, out var excelPid); OfficePids.Add((int)excelPid);
            dynamic book = excel.Workbooks.Add();
            dynamic sheet = book.Worksheets[1];
            const int rows = 20000;
            sheet.Range["A1:H" + rows].Formula = "=ROW()*COLUMN()+0.5";
            sheet.Range["A1:H" + rows].Copy();
            Thread.Sleep(800);
            ClearTarget(target);
            TypeInto(target, ";sig");
            var sw = Stopwatch.StartNew();
            var ok = Ui.WaitTrue(() => TargetText(target).StartsWith(SigBody), 30000, 10);
            var ms = sw.ElapsedMilliseconds;
            Thread.Sleep(2000);
            var back = Clip.GetText() ?? "";
            var lines = back.Split((char)10, StringSplitOptions.RemoveEmptyEntries).Length;
            book.Close(false);
            return ok && ms < 1000 && lines == rows ? $"ok, {rows:N0}×8 range: {ms} ms, range back on the clipboard"
                                                    : $"{rows:N0}×8 range: {(ok ? ms + " ms" : "no expansion in 30 s")}, clipboard has {lines:N0} of {rows:N0} rows";
        }
        finally { EndOffice(excel, (IntPtr)(int)excel.Hwnd); }
    }

    private static string SearchApp()
    {
        // Start opens on the key's release; right after another app closes the
        // first press can be swallowed, so press again until Start is in front.
        var opened = false;
        for (var attempt = 0; attempt < 3 && !opened; attempt++)
        {
            Thread.Sleep(800);
            Native.TapWindowsKey();
            opened = Ui.WaitTrue(() => ForegroundIsOurs().ok, 2500, 100);
        }
        if (!opened) return "n/a here: Start did not come to the front (" + ForegroundIsOurs().who + ")";
        Thread.Sleep(700);
        Native.Type(";date", Layout());
        Thread.Sleep(1500);
        var focused = AutomationElement.FocusedElement;
        var text = focused is null ? "" : Ui.Text(focused);
        Native.Save(Native.Capture(new Native.Rect { L = 0, T = 0, R = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width, B = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height }), Out, "windows-search");
        Native.Tap(Native.VK_ESCAPE); Thread.Sleep(500);
        return text == Today ? "ok" : $"search box holds \"{Short(text)}\"";
    }

    // ---------------------------------------------------------------- helpers

    private static readonly string LongBody = string.Concat(Enumerable.Range(0, 1200).Select(i => $"line{i % 10}abc "))[..10_000];
    private const string AltGrBody = "€ @ \\ ~ | µ";

    private static AutomationElement[] Rows(AutomationElement w) =>
        Ui.FindAll(w, ControlType.ListItem).Where(i => i.Current.Name is not ("Snippets" or "Settings" or "About")).ToArray();

    private static AutomationElement Field(AutomationElement w, string name) => Ui.Wait(() => Ui.Find(w, name, ControlType.Edit));

    private static void Select(AutomationElement row)
    {
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Thread.Sleep(400);
    }

    private static void SetTheme(AutomationElement w, string choice)
    {
        var combo = Ui.Wait(() => Ui.Find(w, "Theme", ControlType.ComboBox));
        ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        Thread.Sleep(300);
        var item = Ui.Wait(() => Ui.Find(combo, choice, ControlType.ListItem) ?? Ui.Find(Ui.Root, choice, ControlType.ListItem));
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        try { ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse(); } catch { }
        Thread.Sleep(900);
    }

    private static void SetPrefix(string prefix)
    {
        var w = Ww.OpenMainWindow();
        Ww.Navigate("Settings");
        var box = Field(w, "Snippet prefix");
        Ui.ClickCenter(box);
        Ui.SetValue(box, prefix);
        Native.Tap(Native.VK_RETURN);
        if (!Ui.WaitTrue(() => (string?)Ww.SnippetsJson()["triggerPrefix"] == prefix, 3000))
            throw new InvalidOperationException("prefix did not change to " + prefix);
        Ww.Navigate("Snippets");
    }

    private static bool ToggleSnippets()
    {
        var items = Tray.OpenMenu(Ww.Pid);
        var item = items?.FirstOrDefault(i => i.Current.Name == "Snippets on");
        if (item is null) return false;
        Ui.Invoke(item);
        Thread.Sleep(300);
        Tray.CloseOverflow();
        return true;
    }

    private sealed record TargetWindow(Process Process, AutomationElement Window, AutomationElement Box);

    private static TargetWindow StartTarget(string klid, bool slowPaste = false)
    {
        var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--target {klid}" + (slowPaste ? " --slow-paste" : "")) { UseShellExecute = false })!;
        Spawned.Add(p);
        var w = Ui.Wait(() => Ui.TopWindow(p.Id, Target.Title + " " + klid), 10000);
        var box = Ui.Wait(() => Ui.Find(w, automationId: "TargetBox"));
        Thread.Sleep(300);
        return new TargetWindow(p, w, box);
    }

    private static void StopTarget(TargetWindow t) { try { t.Process.CloseMainWindow(); t.Process.WaitForExit(3000); if (!t.Process.HasExited) t.Process.Kill(); } catch { } }

    private static void TypeInto(TargetWindow t, string text)
    {
        Ui.ClickCenter(t.Box);
        Thread.Sleep(150);
        Native.Type(text, Layout(t));
    }

    private static void ClearTarget(TargetWindow t) { Ui.SetValue(t.Box, ""); Thread.Sleep(100); }

    private static string TargetText(TargetWindow t) => Ui.Text(t.Box).Replace("\r\n", "\n");

    private static IntPtr Layout(TargetWindow t)
    {
        var thread = Native.GetWindowThreadProcessId(Ui.Hwnd(t.Window), out _);
        return Native.GetKeyboardLayout(thread);
    }

    private static IntPtr Layout()
    {
        var thread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
        return Native.GetKeyboardLayout(thread);
    }

    private static string Describe(string c, IntPtr hkl)
    {
        var r = Native.VkKeyScanEx(c[0], hkl);
        var mods = (r >> 8) & 0xFF;
        var name = ((System.Windows.Forms.Keys)(r & 0xFF)).ToString();
        return ((mods & 6) == 6 ? "AltGr+" : "") + ((mods & 1) != 0 ? "Shift+" : "") + name;
    }

    private static bool TaskbarIsLight()
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return (k?.GetValue("SystemUsesLightTheme") as int?) == 1;
    }

    private static int Luminance(IntPtr hwnd)
    {
        Native.GetWindowRect(hwnd, out var r);
        // A strip of the content area, right of the navigation pane.
        var strip = new Native.Rect { L = r.L + r.W / 2, T = r.B - r.H / 6, R = r.R - 30, B = r.B - 20 };
        using var bmp = Native.Capture(strip);
        long sum = 0; var n = 0;
        for (var x = 0; x < bmp.Width; x += 4) for (var y = 0; y < bmp.Height; y += 4)
        { var c = bmp.GetPixel(x, y); sum += (int)(0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B); n++; }
        return (int)(sum / Math.Max(1, n));
    }

    private static int Lum(string note) => int.Parse(note[(note.LastIndexOf(' ') + 1)..]);

    private static double Difference(System.Drawing.Bitmap a, System.Drawing.Bitmap b)
    {
        using (b)
        {
            long sum = 0; var n = 0;
            for (var x = 0; x < Math.Min(a.Width, b.Width); x += 3)
            for (var y = 0; y < Math.Min(a.Height, b.Height); y += 3)
            {
                var p = a.GetPixel(x, y); var q = b.GetPixel(x, y);
                sum += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B); n++;
            }
            return sum / (3.0 * Math.Max(1, n));
        }
    }

    private static List<string> TopNames() =>
        Ui.Root.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>()
            .Select(e => { try { return $"{e.Current.ClassName}:{e.Current.Name}"; } catch { return ""; } }).ToList();

    /// <summary>The foreground window belongs to the runner's own work: the
    /// Wordwright instance under test, a window the runner started, an Office app
    /// it opened, its scratch files in Notepad or VS Code, or the taskbar, tray
    /// and Start search. Anything else is the person's own window.</summary>
    private static (bool ok, string who) ForegroundIsOurs()
    {
        var hwnd = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(hwnd, out var upid);
        var pid = (int)upid;
        string name = "?", title = "", cls = "";
        try { using var p = Process.GetProcessById(pid); name = p.ProcessName; } catch { }
        try { var e = AutomationElement.FromHandle(hwnd); title = e.Current.Name ?? ""; cls = e.Current.ClassName ?? ""; } catch { }
        bool ours;
        lock (Ww.AllPids) ours = Ww.AllPids.Contains(pid);
        ours |= Spawned.Any(p => { try { return p.Id == pid; } catch { return false; } });
        ours |= OfficePids.Contains(pid);
        ours |= title.Contains("notepad.txt") || title.Contains("vscode.txt") || title == "e2e";
        ours |= name is "SearchHost" or "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchApp";
        ours |= name == "explorer" && cls is "Shell_TrayWnd" or "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow" or "Shell_SecondaryTrayWnd";
        return (ours, $"\"{title}\" ({name})");
    }

    private static string ForegroundName()
    {
        try { return AutomationElement.FromHandle(Native.GetForegroundWindow()).Current.Name; } catch { return "?"; }
    }

    /// <summary>Ends an Office app the runner started through COM: Quit, and if
    /// the process is still there a moment later, end it by its window's process id.</summary>
    private static void EndOffice(dynamic app, IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        try { app.Quit(0); } catch { }
        try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(app); } catch { }
        GC.Collect(); GC.WaitForPendingFinalizers();
        if (pid == 0) return;
        try { using var p = Process.GetProcessById((int)pid); if (!p.WaitForExit(5000)) p.Kill(); } catch (ArgumentException) { }
    }

    private static void ScreenShot(string name)
    {
        var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        Native.Save(Native.Capture(new Native.Rect { L = b.Left, T = b.Top, R = b.Right, B = b.Bottom }), Out, name);
    }

    /// <summary>Puts text on the clipboard from a helper process that keeps a
    /// message loop running, as a real app does. When the runner itself owns the
    /// clipboard without pumping messages, Windows makes Wordwright wait for it
    /// (EmptyClipboard asks the owner first), which no real app would cause.</summary>
    private static Process OwnClipboard(string text)
    {
        var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--clip-owner-text " + Convert.ToBase64String(Encoding.UTF8.GetBytes(text))) { UseShellExecute = false, RedirectStandardOutput = true })!;
        Spawned.Add(p);
        p.StandardOutput.ReadLine();
        Thread.Sleep(300);
        return p;
    }

    private static void Shot(IntPtr hwnd, string name)
    {
        Native.GetWindowRect(hwnd, out var r);
        Native.Save(Native.Capture(r), Out, name);
    }

    private static Result Pass(string d) => new("", "", "Pass", d);
    private static Result Fail(string d) => new("", "", "Fail", d);

    private static void Check(string id, string check, Func<Result> run)
    {
        if (Only is not null && !Only.Contains(id) && !Only.Any(o => o.StartsWith(id + ":"))) return;
        Console.Write($"{id,-5} {check} ... ");
        Result r;
        try { r = run(); }
        catch (ForeignWindowException) { throw; }
        catch (Exception ex)
        {
            var frame = ex.StackTrace?.Split((char)10).FirstOrDefault(l => l.Contains("Program.cs")) ?? "";
            var line = frame.Contains(":line ") ? " (Program.cs line " + frame[(frame.LastIndexOf(":line ") + 6)..].Trim() + ")" : "";
            r = Fail(ex.GetType().Name + ": " + ex.Message + line);
        }
        if (r.Outcome == "Fail") ScreenShot("fail-" + id);
        Add(id, check, r.Outcome, r.Detail);
        Console.WriteLine(r.Outcome);
        if (r.Outcome != "Pass") Console.WriteLine("      " + r.Detail);
    }

    private static void Add(string id, string check, string outcome, string detail) => Results.Add(new Result(id, check, outcome, detail));

    private static Result Axe(string name)
    {
        var (n, d) = AxeScan(name);
        return n == 0 ? Pass("0 errors") : Fail($"{n} errors: {d}");
    }

    private static (int errors, string detail) AxeScan(string name)
    {
        var dir = Path.Combine(Out, "axe");
        var config = Config.Builder.ForProcessId(Ww.Pid).WithOutputFileFormat(OutputFileFormat.A11yTest).WithOutputDirectory(dir).Build();
        var scanner = ScannerFactory.CreateScanner(config);
        var output = scanner.Scan(new ScanOptions(scanId: name));
        var errors = output.WindowScanOutputs.SelectMany(o => o.Errors).ToList();
        var detail = string.Join(", ", errors.Select(e => $"{e.Rule.ID} on {e.Element.Properties.GetValueOrDefault("ControlType", "?")} \"{e.Element.Properties.GetValueOrDefault("Name", "")}\"").Distinct().Take(8));
        return (errors.Count, detail);
    }

    private static Result LogHasNoContent()
    {
        var log = Ww.LogText();
        var leaks = new[] { SigBody, "Jaspreet", "line1abc", "E2E clipboard marker", "€", ";sig", "Thanks quit", "E2E long", Today }
            .Where(s => log.Contains(s, StringComparison.Ordinal)).ToList();
        var events = log.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => l.Split(' ', 3).Last()).Distinct().Take(12);
        return leaks.Count == 0 ? Pass($"{log.Split('\n').Length} lines; none hold typed, clipboard or snippet text; events: {string.Join(", ", events)}")
                                : Fail("found in the log: " + string.Join(", ", leaks));
    }

    private static string Short(string s) => s.Length > 60 ? s[..60].Replace("\n", "⏎") + "…" : s.Replace("\n", "⏎");

    private static void Cleanup(string? savedClipboard, bool capsWasOn)
    {
        Ww.Stop();
        foreach (var p in Spawned) { try { if (!p.HasExited) p.Kill(true); } catch { } }
        if (((Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0) != capsWasOn) Native.Tap(Native.VK_CAPITAL);
        if (savedClipboard is not null) Clip.SetText(savedClipboard);
    }

    private static void WriteReport(string exe)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Wordwright end-to-end run, {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine($"Build: `{exe}` ({File.GetLastWriteTime(exe):yyyy-MM-dd HH:mm}). Screenshots are next to this file.");
        sb.AppendLine();
        sb.AppendLine("| # | Check | Result | Detail |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var r in Results) sb.AppendLine($"| {r.Id} | {r.Check} | {r.Outcome} | {r.Detail.Replace("|", "\\|")} |");
        File.WriteAllText(Path.Combine(Out, "report.md"), sb.ToString());
        Console.WriteLine();
        Console.WriteLine($"{Results.Count(r => r.Outcome == "Pass")} passed, {Results.Count(r => r.Outcome == "Fail")} failed. Report: {Path.Combine(Out, "report.md")}");
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Wordwright.slnx"))) d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }
}
