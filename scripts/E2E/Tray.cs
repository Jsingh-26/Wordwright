using System.Windows.Automation;

namespace E2E;

/// <summary>
/// Finds the test instance's tray icon among the notification-area buttons by its
/// tooltip (the test instance uses the default ";" prefix, so it is told apart
/// from an installed copy), opening the hidden-icons flyout when it is there.
/// </summary>
internal static class Tray
{
    public const string TooltipStart = "Wordwright: type ;";

    public static AutomationElement? FindIcon(bool openOverflow = true)
    {
        var icon = Search();
        if (icon is not null || !openOverflow) return icon;

        var chevron = FindChevron();
        if (chevron is null) return null;
        Ui.ClickCenter(chevron);
        Thread.Sleep(800);
        return Search();
    }

    /// <summary>Closes the hidden-icons flyout if it is open.</summary>
    public static void CloseOverflow()
    {
        var fg = Native.GetForegroundWindow();
        string cls;
        try { cls = AutomationElement.FromHandle(fg).Current.ClassName ?? ""; } catch { return; }
        if (cls is "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow")
        {
            Native.Tap(Native.VK_ESCAPE);
            Thread.Sleep(300);
        }
    }

    private static AutomationElement? Search()
    {
        foreach (var top in Ui.Root.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>())
        {
            string cls;
            try { cls = top.Current.ClassName ?? ""; } catch (ElementNotAvailableException) { continue; }
            if (cls is not ("Shell_TrayWnd" or "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow" or "Shell_SecondaryTrayWnd"))
                continue;
            foreach (var b in top.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>())
            {
                string name;
                try { name = b.Current.Name ?? ""; } catch (ElementNotAvailableException) { continue; }
                if (name.StartsWith(TooltipStart, StringComparison.Ordinal) && !b.Current.BoundingRectangle.IsEmpty)
                    return b;
            }
        }
        return null;
    }

    private static AutomationElement? FindChevron()
    {
        var tray = Ui.Root.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        if (tray is null) return null;
        foreach (var b in tray.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>())
        {
            var name = b.Current.Name ?? "";
            var id = b.Current.AutomationId ?? "";
            if (name.Contains("hidden icons", StringComparison.OrdinalIgnoreCase) || id.Contains("NotifyItemChevron", StringComparison.OrdinalIgnoreCase)
                || id.Contains("SystemTrayIcon", StringComparison.OrdinalIgnoreCase) && name.Contains("Show", StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    /// <summary>Right-clicks the icon and returns the menu's items once the menu is up.</summary>
    public static AutomationElement[]? OpenMenu(int pid)
    {
        var icon = FindIcon();
        if (icon is null) return null;
        Ui.ClickCenter(icon, right: true);
        try
        {
            return Ui.Wait(() =>
            {
                var items = Ui.TopWindows(pid).SelectMany(w => Ui.FindAll(w, ControlType.MenuItem)).ToArray();
                return items.Length > 0 ? items : null;
            }, 4000);
        }
        catch (TimeoutException) { return null; }
    }

    /// <summary>The brightest pixel inside the icon, 0–255: the glyph at full
    /// strength is near 255 on a dark taskbar, and about half when dimmed.</summary>
    public static (int max, string path) MeasureGlyph(string outDir, string name)
    {
        var icon = FindIcon();
        if (icon is null) return (-1, "");
        var r = Ui.RectOf(icon);
        // The glyph sits in the middle of the button; measure the middle 60 %.
        var inner = new Native.Rect { L = r.L + r.W / 5, T = r.T + r.H / 5, R = r.R - r.W / 5, B = r.B - r.H / 5 };
        using var bmp = Native.Capture(inner);
        int max = 0, min = 255;
        for (var x = 0; x < bmp.Width; x++)
        for (var y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            var l = (int)(0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B);
            max = Math.Max(max, l); min = Math.Min(min, l);
        }
        var wide = new Native.Rect { L = r.L - 4, T = r.T - 4, R = r.R + 4, B = r.B + 4 };
        var path = Native.Save(Native.Capture(wide), outDir, name);
        return (max, path);
    }
}
