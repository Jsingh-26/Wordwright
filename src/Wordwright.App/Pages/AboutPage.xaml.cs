using System.Windows.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Wordwright.App.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    // The only allowed navigation: open the repository in the user's browser.
    // (Rule 1: the app itself never makes network requests.)
    private void OnSourceLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}