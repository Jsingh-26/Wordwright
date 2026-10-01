using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Wordwright.App.Pages;
using Wordwright.Core.Settings;

namespace Wordwright.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        RestorePlacement();
        Loaded += OnLoaded;
    }

    /// <summary>Which page the window opens on; the welcome can ask for another
    /// one. It has to wait for Loaded — navigating before then throws.</summary>
    private Type _startPage = typeof(SnippetsPage);

    /// <summary>The Snippets page while it is on screen, so the window's
    /// accelerators can reach it. A page registers itself as it loads.</summary>
    private SnippetsPage? _snippetsPage;

    /// <summary>What an accelerator asked for while another page was on screen,
    /// run once the Snippets page has loaded.</summary>
    private Action<SnippetsPage>? _pendingSnippetsAction;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(_startPage);
    }

    /// <summary>Open on a particular page the next time the window appears.</summary>
    internal void ShowPage(Type pageType) => _startPage = pageType;

    /// <summary>The Snippets page calls this when it appears
    /// (docs/PLAN.md → P3.4a).</summary>
    internal void OnSnippetsPageLoaded(SnippetsPage page)
    {
        _snippetsPage = page;

        var pending = _pendingSnippetsAction;
        _pendingSnippetsAction = null;
        pending?.Invoke(page);
    }

    private void OnNewSnippetCommand(object sender, ExecutedRoutedEventArgs e)
        => OnSnippetsPage(page => page.NewSnippet());

    private void OnFindCommand(object sender, ExecutedRoutedEventArgs e)
        => OnSnippetsPage(page => page.FocusSearch());

    private void OnDeleteSnippetCommand(object sender, ExecutedRoutedEventArgs e)
        => OnSnippetsPage(page => page.ConfirmDeleteSelected());

    /// <summary>Runs one of the window's accelerators on the Snippets page,
    /// switching to it first when another page is on screen.</summary>
    private void OnSnippetsPage(Action<SnippetsPage> action)
    {
        if (_snippetsPage is { IsVisible: true } page)
        {
            action(page);
            return;
        }

        _pendingSnippetsAction = action;
        RootNavigation.Navigate(typeof(SnippetsPage));
    }

    /// <summary>Puts the window back where it was when it last closed, pulled
    /// inside the current work area (docs/PLAN.md → P3.4a).</summary>
    private void RestorePlacement()
    {
        var workArea = ToArea(SystemParameters.WorkArea);
        var saved = ((App)Application.Current).Settings.Window;

        if (saved is null)
        {
            // First run: the designed size, centred by WindowStartupLocation, but
            // never taller than the work area — a centred 720 px window in a
            // shorter work area would push its title bar off the top.
            var initial = new WindowPlacement().ClampTo(workArea);
            Width = initial.Width;
            Height = initial.Height;
            return;
        }

        var placement = saved.ClampTo(workArea);

        // Left and Top are only honoured with a manual start-up location, and both
        // are applied when the window is shown, so nothing jumps into place later.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;

        if (placement.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Remembers the window's position and size for the next launch. A
    /// maximised window is remembered by the size it returns to.</summary>
    private void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (bounds.IsEmpty || double.IsNaN(bounds.Width))
        {
            // Never shown, so there is nothing worth remembering.
            return;
        }

        var app = (App)Application.Current;

        app.UpdateSettings(app.Settings with
        {
            Window = new WindowPlacement
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = WindowState == WindowState.Maximized,
            },
        });
    }

    private static WindowArea ToArea(Rect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);

    // Closing hides to the tray; the app keeps running. The tray menu's Quit exits.
    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
