using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using Wordwright.App.Resources;

namespace Wordwright.App.Pages;

public partial class AboutPage : Page
{
    /// <summary>The "Support Wordwright" page (decision D12, P10.5): the Razorpay
    /// Payment Page, opened in the browser. Empty until that page exists, and the
    /// link stays hidden while it is.</summary>
    private const string SupportUrl = "";

    public AboutPage()
    {
        InitializeComponent();

        if (SupportUrl.Length > 0)
        {
            SupportHyperlink.NavigateUri = new Uri(SupportUrl);
            SupportLink.Visibility = Visibility.Visible;
        }

        if (Version() is { } version)
        {
            VersionText.Text = Strings.Get("About.Version", ("Version", version));
        }

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded runs before the first layout pass, and the templates below only
        // exist after that, so wait for the dispatcher to catch up.
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                // WPF-UI's CardExpander builds its toggle button in its own
                // template and leaves it unnamed, so a screen reader reads it as
                // "button". Its header is the name it should carry.
                Accessibility.Name(this.Part("ExpanderToggleButton"), "About.ThirdParty");
            },
            DispatcherPriority.Loaded);

        // Page entrance (docs/PLAN.md → P11.5).
        Motion.Enter(this);
    }

    /// <summary>The app's version, as "1.0.0" (docs/DESIGN.md §9), or null when
    /// the assembly carries none.</summary>
    private static string? Version()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;

        return version is null ? null : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    // The only allowed navigation: open the repository or the support page in
    // the user's browser.
    // (Rule 1: the app itself never makes network requests.)
    private void OnSourceLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}