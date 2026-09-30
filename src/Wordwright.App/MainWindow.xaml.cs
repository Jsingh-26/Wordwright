using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using Wordwright.App.Pages;

namespace Wordwright.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Keep the whole window, including its title bar, on screen: centring a
        // 720 px-tall window in a shorter work area would push it above the top,
        // which hides the window buttons.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);

        Loaded += OnLoaded;
    }

    /// <summary>Which page the window opens on; the welcome can ask for another
    /// one. It has to wait for Loaded — navigating before then throws.</summary>
    private Type _startPage = typeof(SnippetsPage);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(_startPage);
    }

    /// <summary>Open on a particular page the next time the window appears.</summary>
    internal void ShowPage(Type pageType) => _startPage = pageType;

    // Closing hides to the tray; the app keeps running. The tray menu's Quit exits.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}