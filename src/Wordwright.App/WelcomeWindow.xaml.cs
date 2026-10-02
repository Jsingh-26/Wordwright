using System.Windows;
using System.Windows.Automation;
using Wpf.Ui.Controls;
using Wordwright.App.Resources;

namespace Wordwright.App;

/// <summary>
/// The one-time welcome of docs/UX_COPY.md "First run". It points at the tray
/// icon and the two things a new user can do, and it lets them try a snippet
/// where they are standing rather than sending them to another app
/// (docs/PLAN.md → P3.4c).
/// </summary>
public partial class WelcomeWindow : FluentWindow
{
    private readonly App _app;

    public WelcomeWindow()
    {
        InitializeComponent();

        _app = (App)Application.Current;

        var prefix = _app.Snippets.TriggerPrefix;
        BodyText.Text = Strings.Get("Welcome.Body", ("Prefix", prefix));

        var tryHere = Strings.Get("Welcome.TryHere", ("Prefix", prefix));
        TryHereLabel.Text = tryHere;
        AutomationProperties.SetName(TryHereBox, tryHere);

        // The first expansion in this box is the whole point of the playground,
        // so acknowledge it. The engine is rebuilt if the excluded apps change,
        // hence reading it from the app rather than keeping it.
        if (_app.SnippetEngine is { } engine)
        {
            engine.Expanded += OnSnippetExpanded;
        }

        Closed += OnClosed;
    }

    /// <summary>Once a snippet has expanded in the box itself, the user has seen
    /// it work, so the line says so — arriving with the check-circle and the one
    /// entrance animation rather than snapping (docs/PLAN.md → P11.6).</summary>
    private void OnSnippetExpanded(object? sender, EventArgs e)
    {
        if (TryHereBox.IsKeyboardFocusWithin)
        {
            TryHereCheck.Visibility = Visibility.Visible;
            TryHereLabel.Text = Strings.Get("Welcome.TryHere.Done");
            Motion.Enter(TryHereRow);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_app.SnippetEngine is { } engine)
        {
            engine.Expanded -= OnSnippetExpanded;
        }
    }

    private void OnShowSnippetsClicked(object sender, RoutedEventArgs e)
    {
        Close();
        _app.ShowMainWindow();
    }
}
