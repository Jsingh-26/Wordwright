using System.Windows;
using System.Windows.Controls;
using Wordwright.Core.Settings;

namespace Wordwright.App.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        StartWithWindowsToggle.IsChecked = ((App)Application.Current).Settings.StartWithWindows;
    }

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var enabled = StartWithWindowsToggle.IsChecked == true;

        // UpdateSettings persists the setting and keeps the HKCU Run key in sync.
        app.UpdateSettings(app.Settings with { StartWithWindows = enabled });
    }
}