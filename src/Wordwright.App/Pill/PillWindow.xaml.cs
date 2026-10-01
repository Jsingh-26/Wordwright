using System.Windows;
using System.Windows.Media;
using Wordwright.App.Resources;

namespace Wordwright.App.Pill;

/// <summary>The state the pill is showing.</summary>
public enum PillState
{
    /// <summary>Ember dot and "Rewriting… N s", with the ruler tick beneath.</summary>
    Working,

    /// <summary>The model is loading before the rewrite can start.</summary>
    Loading,

    /// <summary>A check mark and "Done. Ctrl+Z undoes it.", kept for 3 s.</summary>
    Done,

    /// <summary>A short error, shown the same way as Done.</summary>
    Error,
}

/// <summary>
/// The progress pill (docs/DESIGN.md §7, docs/PLAN.md P6.6): a 32 px floating
/// window near the caret that shows the rewrite's progress, cancels on Esc, and
/// shows the short result. Under the text sits the ruler tick (decision D2): a
/// 1 px Steel track whose accent fill grows over the expected time for this
/// input, and turns Ember when it runs past the estimate.
/// <para>
/// It never takes focus (docs/DESIGN.md §7), so it is an unfocusable window and
/// the Esc key is heard by a global watcher instead.
/// </para>
/// </summary>
public partial class PillWindow : Window
{
    private const int PillHeight = 32;

    /// <summary>How long the "done" or error text stays up.</summary>
    private static readonly TimeSpan ResultLinger = TimeSpan.FromSeconds(3);

    /// <summary>The one case UX_COPY.md gives six seconds: the copy fallback.</summary>
    private static readonly TimeSpan CopiedLinger = TimeSpan.FromSeconds(6);

    private readonly RulerTick _ruler;

    private System.Windows.Threading.DispatcherTimer? _elapsedTimer;

    /// <summary>Esc was pressed while the pill was up.</summary>
    public event EventHandler? CancelRequested;

    private PillState _state = PillState.Working;

    public PillWindow()
    {
        InitializeComponent();

        Height = PillHeight;
        // The pill must never steal the caret from the app being rewritten.
        ShowActivated = false;
        Focusable = false;

        _ruler = Ruler;
        _ruler.AnimationsEnabled = SystemParameters.ClientAreaAnimation;
    }

    /// <summary>The expected time for this rewrite, used by the ruler tick.</summary>
    public TimeSpan Estimate { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Places the pill near the caret and starts the elapsed clock.</summary>
    public void Start(double caretLeft, double caretTop)
    {
        Left = caretLeft;
        Top = caretTop + 20;

        _elapsedTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
        _elapsedTimer.Start();

        UpdateElapsed();
    }

    /// <summary>The Esc watcher reports a press; only the working/loading pill
    /// treats it as a cancel.</summary>
    internal void ReportEscape()
    {
        if (_state is PillState.Working or PillState.Loading)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Shows "Loading the AI model…" before generation begins.</summary>
    public void ShowLoading()
    {
        _state = PillState.Loading;
        Dot.Fill = (Brush)FindResource("BrandAccentBrush");
        Message.Text = Strings.Get("Pill.Loading");
        Cancel.Visibility = Visibility.Visible;
        Ruler.Visibility = Visibility.Collapsed;
    }

    /// <summary>Shows "Rewriting… N s" with the ruler tick.</summary>
    public void ShowWorking()
    {
        _state = PillState.Working;
        Dot.Fill = (Brush)FindResource("EmberBrush");
        Cancel.Text = Strings.Get("Pill.Cancel");
        Cancel.Visibility = Visibility.Visible;
        // The ruler tick is skipped when Windows animations are off (D2), so
        // nothing appears to move on its own.
        Ruler.Visibility = _ruler.AnimationsEnabled ? Visibility.Visible : Visibility.Collapsed;
        _startedAt = DateTimeOffset.Now;
        UpdateElapsed();
    }

    /// <summary>Shows a short result and closes itself after the linger.</summary>
    public void ShowResult(PillState state, string message, bool sixSeconds = false)
    {
        _state = state;
        _elapsedTimer?.Stop();

        Dot.Visibility = Visibility.Collapsed;
        Glyph.Visibility = Visibility.Visible;

        if (state == PillState.Done)
        {
            // A check mark, drawn rather than taken from an icon font.
            Glyph.Data = Geometry.Parse("M2,8 L6,12 L14,3");
            Glyph.Stroke = (Brush)FindResource("BrandAccentBrush");
        }
        else
        {
            Glyph.Data = Geometry.Parse("M8,2 L8,9 M8,12 L8,13");
            Glyph.Stroke = (Brush)FindResource("CautionBrush");
        }

        Message.Text = message;
        Cancel.Visibility = Visibility.Collapsed;
        Ruler.Visibility = Visibility.Collapsed;

        var linger = sixSeconds ? CopiedLinger : ResultLinger;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = linger };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    private DateTimeOffset _startedAt;

    private void UpdateElapsed()
    {
        if (_state is not (PillState.Working or PillState.Loading))
        {
            return;
        }

        var elapsed = DateTimeOffset.Now - _startedAt;
        var seconds = (int)Math.Round(elapsed.TotalSeconds);
        Message.Text = Strings.Get("Pill.Working", ("Seconds", seconds));

        if (_state == PillState.Working)
        {
            _ruler.Progress = Estimate.TotalSeconds <= 0 ? 0 : elapsed.TotalSeconds / Estimate.TotalSeconds;
        }
    }
}
