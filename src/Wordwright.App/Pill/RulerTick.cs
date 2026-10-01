using System.Windows;
using System.Windows.Media;

namespace Wordwright.App.Pill;

/// <summary>
/// The pill's ruler tick (docs/DESIGN.md §7, decision D2 in docs/PLAN.md P6.6):
/// a 1 px Steel track with a Forge-ink (or Ink-light) fill that grows over the
/// expected time for this input. Past the estimate the fill continues in Ember
/// instead of resetting, so a rewrite that overruns says so at a glance.
/// <para>
/// The fill is skipped when Windows animations are off: the track shows, the
/// fill jumps to the finished width, and nothing moves on its own.
/// </para>
/// </summary>
public sealed class RulerTick : FrameworkElement
{
    private const double TrackHeight = 1;

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(RulerTick),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How far along the estimate, 0 to 1; above 1 is past the estimate.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Whether the fill animates; false when Windows animations are off.</summary>
    public bool AnimationsEnabled { get; set; } = true;

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var track = Fill("SelectionBrush", Color.FromRgb(0xE9, 0xEC, 0xF3));
        context.DrawRectangle(track, null, new Rect(0, (ActualHeight - TrackHeight) / 2, width, TrackHeight));

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
        {
            return;
        }

        // Within the estimate the fill is the brand accent; past it, Ember.
        var brush = Progress > 1
            ? Fill("EmberBrush", Color.FromRgb(0xC7, 0x62, 0x1E))
            : Fill("BrandAccentBrush", Color.FromRgb(0x23, 0x40, 0x8E));

        context.DrawRectangle(brush, null, new Rect(0, (ActualHeight - TrackHeight) / 2, width * progress, TrackHeight));
    }

    private Brush Fill(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
