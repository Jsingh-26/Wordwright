using System.Windows;
using Wpf.Ui.Controls;
using Wordwright.App.Pages;
using Wordwright.App.Resources;

namespace Wordwright.App;

/// <summary>
/// The one-time welcome of docs/UX_COPY.md "First run". It points at the tray
/// icon and the two things a new user can do, then gets out of the way.
/// </summary>
public partial class WelcomeWindow : FluentWindow
{
    public WelcomeWindow()
    {
        InitializeComponent();

        BodyText.Text = Strings.Get(
            "Welcome.Body", ("Prefix", ((App)Application.Current).Snippets.TriggerPrefix));
    }

    private void OnShowSnippetsClicked(object sender, RoutedEventArgs e)
    {
        Close();
        ((App)Application.Current).ShowMainWindow();
    }

    private void OnTurnOnAiClicked(object sender, RoutedEventArgs e)
    {
        Close();
        ((App)Application.Current).ShowMainWindow(typeof(OfflineAiPage));
    }
}
