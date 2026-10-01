using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Wordwright.App.Controls;

/// <summary>One estimate drawn on the ruler: a Steel band from its fastest to
/// its slowest second, with the job's name and range beside it.</summary>
public sealed record RulerBand(string Label, string Range, int FastestSeconds, int SlowestSeconds);

/// <summary>
/// The time ruler of docs/DESIGN.md §5: a second scale along the top, and one
/// band per estimate. The bands are drawn once, left to right, when the ruler
/// appears — and appear at once when Windows animations are turned off.
/// A band that runs past the ruler's end is clamped and its caption says
/// "30+" (docs/DESIGN.md: the scale stops at 30 seconds).
/// </summary>
public sealed class TimeRuler : FrameworkElement
{
    /// <summary>The furthest the scale goes, in seconds.</summary>
    public const int MaximumSeconds = 30;

    private const double TickRowHeight = 20;
    private const double BandRowHeight = 18;
    private const double BandThickness = 8;
    private const int AnimationMilliseconds = 400;

    public static readonly DependencyProperty BandsProperty = DependencyProperty.Register(
        nameof(Bands),
        typeof(IReadOnlyList<RulerBand>),
        typeof(TimeRuler),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How much of the ruler to draw, 0 to 1; animated from 0.</summary>
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(TimeRuler),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<RulerBand>? Bands
    {
        get => (IReadOnlyList<RulerBand>?)GetValue(BandsProperty);
        set => SetValue(BandsProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>The second the scale ends at for these bands: the slowest
    /// estimate, never past 30.</summary>
    private int ScaleSeconds =>
        Math.Clamp(Bands?.Count > 0 ? Bands.Max(band => band.SlowestSeconds) : 0, 2, MaximumSeconds);

    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = Math.Max(1, Bands?.Count ?? 0);

        return new Size(
            double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width,
            TickRowHeight + rows * BandRowHeight);
    }

    protected override void OnRender(DrawingContext context)
    {
        var scale = ScaleSeconds;
        var width = ActualWidth;
        var bandArea = width * Math.Clamp(Progress, 0, 1);

        var tickText = TextBrush("TextFillColorSecondaryBrush");
        var bandBrush = Fill("SelectionBrush", Color.FromRgb(0xE9, 0xEC, 0xF3));
        var bandPen = new Pen(Fill("BrandAccentBrush", Color.FromRgb(0x23, 0x40, 0x8E)), 1.5);

        // The scale: a tick every two seconds, labelled.
        for (var second = 0; second <= scale; second += 2)
        {
            var x = X(second, scale, width);

            context.DrawLine(new Pen(tickText, 1), new Point(x, TickRowHeight - 6), new Point(x, TickRowHeight));

            var label = second == 0
                ? "0s"
                : second.ToString(CultureInfo.CurrentCulture);
            context.DrawText(Formatted(label, tickText), new Point(x - 4, 0));
        }

        if (Bands is not { } bands)
        {
            return;
        }

        // The bands, clipped to however much has been drawn so far.
        context.PushClip(new RectangleGeometry(new Rect(0, TickRowHeight, bandArea, ActualHeight - TickRowHeight)));

        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];
            var top = TickRowHeight + index * BandRowHeight + (BandRowHeight - BandThickness) / 2;

            var from = X(Math.Min(band.FastestSeconds, scale), scale, width);
            var to = X(Math.Min(band.SlowestSeconds, scale), scale, width);
            var rectangle = new Rect(from, top, Math.Max(2, to - from), BandThickness);

            context.DrawRoundedRectangle(bandBrush, bandPen, rectangle, 2, 2);
        }

        context.Pop();

        // The captions sit after the widest band, outside the clip so they do not
        // fade in with it.
        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];
            var top = TickRowHeight + index * BandRowHeight;

            var caption = Formatted($"{band.Label}: {band.Range}", TextBrush("TextFillColorPrimaryBrush"));
            context.DrawText(caption, new Point(Math.Min(width - caption.Width, bandArea + 8), top));
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (!SystemParameters.ClientAreaAnimation)
        {
            // Windows animations are off: show the finished ruler immediately.
            Progress = 1;
            return;
        }

        Progress = 0;
        Loaded += (_, _) =>
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation(
                0, 1, TimeSpan.FromMilliseconds(AnimationMilliseconds))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                },
            };

            BeginAnimation(ProgressProperty, animation);
        };
    }

    private static double X(int seconds, int scale, double width) =>
        scale <= 0 ? 0 : seconds / (double)scale * width;

    private FormattedText Formatted(string text, Brush brush) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(
            (FontFamily)TryFindResource("AppText") ?? new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal),
        12,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    /// <summary>A theme brush, with a fallback for the moment before the app's
    /// resources are reachable.</summary>
    private Brush TextBrush(string key) => Fill(key, Colors.Gray);

    private Brush Fill(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
