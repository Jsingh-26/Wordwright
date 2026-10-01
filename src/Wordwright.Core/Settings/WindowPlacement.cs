namespace Wordwright.Core.Settings;

/// <summary>
/// Where the main window was when it last closed, remembered in
/// <c>settings.json</c> as the <c>window</c> object (docs/PLAN.md → P3.4a).
/// Positions and sizes are in device-independent pixels, the units WPF uses.
/// A maximised window is remembered by the size it would have when restored.
/// </summary>
public sealed record WindowPlacement
{
    /// <summary>The smallest the window may be made (docs/DESIGN.md §2).</summary>
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
    /// The same placement pulled inside a work area: the size stays between the
    /// window's minimum and the work area, and the position moves far enough to
    /// put the whole window on screen. This is what keeps a window saved on a
    /// monitor that is no longer attached — or at 200 % scaling on a smaller
    /// screen — from opening out of reach.
    /// </summary>
    public WindowPlacement ClampTo(WindowArea workArea)
    {
        // A work area smaller than the minimum loses: the window keeps its
        // minimum size and sits at the work area's corner, still usable.
        var width = Math.Clamp(Width, MinimumWidth, Math.Max(MinimumWidth, workArea.Width));
        var height = Math.Clamp(Height, MinimumHeight, Math.Max(MinimumHeight, workArea.Height));

        return this with
        {
            Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width)),
            Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height)),
            Width = width,
            Height = height,
        };
    }
}
