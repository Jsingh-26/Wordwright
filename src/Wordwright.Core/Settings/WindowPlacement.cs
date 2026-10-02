namespace Wordwright.Core.Settings;

/// <summary>
/// Where the main window was when it last closed, remembered in
/// <c>settings.json</c> as the <c>window</c> object (docs/PLAN.md → P3.4a).
/// Positions and sizes are in device-independent pixels, the units WPF uses.
/// A maximised window is remembered by the size it would have when restored.
/// </summary>
public sealed record WindowPlacement
{
    /// <summary>The smallest the window may be made (docs/DESIGN.md §2). The
    /// height gives way on a shorter work area (see <see cref="MinimumHeightFor"/>).</summary>
    public const double MinimumWidth = 800;
    public const double MinimumHeight = 600;

    /// <summary>The size the window opens at the very first time.</summary>
    public const double DefaultWidth = 1040;
    public const double DefaultHeight = 720;

    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; } = DefaultWidth;

    public double Height { get; init; } = DefaultHeight;

    public bool Maximized { get; init; }

    /// <summary>
    /// The minimum height on this work area: 600, or less when the work area is
    /// shorter. A 1366×768 laptop at 125 % has about 582 DIPs to offer, and the
    /// pages scroll, so the window fits rather than hiding its bottom under the
    /// taskbar (docs/PLAN.md P12.9).
    /// </summary>
    public static double MinimumHeightFor(WindowArea workArea) => Math.Min(MinimumHeight, workArea.Height);

    /// <summary>
    /// Of the monitors' work areas, the one this placement belongs on: the one it
    /// overlaps most, or, when it overlaps none (that monitor is gone), the one
    /// nearest its centre. The first area (the primary monitor) wins a tie.
    /// </summary>
    public WindowArea PickWorkArea(IReadOnlyList<WindowArea> workAreas)
    {
        if (workAreas.Count == 0)
        {
            throw new ArgumentException("At least one work area is needed.", nameof(workAreas));
        }

        var best = workAreas[0];
        var bestOverlap = Overlap(best);
        foreach (var area in workAreas.Skip(1))
        {
            var overlap = Overlap(area);
            if (overlap > bestOverlap)
            {
                best = area;
                bestOverlap = overlap;
            }
        }

        if (bestOverlap > 0)
        {
            return best;
        }

        var centreX = Left + Width / 2;
        var centreY = Top + Height / 2;
        return workAreas
            .OrderBy(area => DistanceSquared(area, centreX, centreY))
            .First();
    }

    private double Overlap(WindowArea area)
    {
        var width = Math.Min(Left + Width, area.Right) - Math.Max(Left, area.Left);
        var height = Math.Min(Top + Height, area.Bottom) - Math.Max(Top, area.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }

    private static double DistanceSquared(WindowArea area, double x, double y)
    {
        var dx = Math.Max(Math.Max(area.Left - x, 0), x - area.Right);
        var dy = Math.Max(Math.Max(area.Top - y, 0), y - area.Bottom);
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// The same placement pulled inside a work area: the size stays between the
    /// window's minimum and the work area, and the position moves far enough to
    /// put the whole window on screen. This is what keeps a window saved on a
    /// monitor that is no longer attached — or at 200 % scaling on a smaller
    /// screen — from opening out of reach.
    /// </summary>
    public WindowPlacement ClampTo(WindowArea workArea)
    {
        // A work area narrower than the minimum width loses: the window keeps
        // that width and sits at the work area's left edge, still usable. The
        // height gives way instead, because the pages scroll.
        var minimumHeight = MinimumHeightFor(workArea);
        var width = Math.Clamp(Width, MinimumWidth, Math.Max(MinimumWidth, workArea.Width));
        var height = Math.Clamp(Height, minimumHeight, Math.Max(minimumHeight, workArea.Height));

        return this with
        {
            Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width)),
            Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height)),
            Width = width,
            Height = height,
        };
    }
}
