using System.Windows.Forms;

namespace Wordwright.Platform.Display;

/// <summary>
/// The work area of every monitor, so the main window can come back on the
/// monitor it was closed on (docs/PLAN.md P12.9). The app is per-monitor DPI
/// aware, so Windows answers in physical pixels.
/// </summary>
public static class Monitors
{
    /// <summary>Each monitor's work area (the taskbar taken out), in physical
    /// pixels, primary monitor first.</summary>
    public static IReadOnlyList<(int Left, int Top, int Width, int Height)> WorkAreasInPixels()
    {
        return Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .Select(screen => screen.WorkingArea)
            .Select(area => (area.Left, area.Top, area.Width, area.Height))
            .ToList();
    }
}
