using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Wordwright.App.Resources;
using Wordwright.App.ViewModels;

namespace Wordwright.App.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        ViewModel = new SettingsViewModel((App)Application.Current);
        DataContext = ViewModel;

        // The recorder clashes against an action's own hotkey; the palette hotkey
        // may not take one of those either (docs/UX_COPY.md → Actions.Hotkey.Duplicate).
        PaletteHotkeyBox.Validate = ViewModel.ValidatePaletteHotkey;
    }

    internal SettingsViewModel ViewModel { get; }

    private void OnAddExcludedAppClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("Settings.ExcludedApps.Add"),
            // WPF wants "description|pattern"; the pattern doubles as the
            // description rather than inventing a label UX_COPY.md does not have.
            Filter = "*.exe|*.exe",
            CheckFileExists = true,
        };

        if (Show(dialog) == true)
        {
            ViewModel.AddExcludedApp(dialog.FileName);
        }
    }

    private void OnRemoveExcludedAppClicked(object sender, RoutedEventArgs e)
    {
        // Qualified: Wpf.Ui.Controls.Button is also in scope in the XAML's code-behind.
        if (sender is System.Windows.Controls.Button { Tag: string executable })
        {
            ViewModel.RemoveExcludedApp(executable);
        }
    }

    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("Settings.Export"),
            FileName = "wordwright-snippets.json",
            DefaultExt = ".json",
            Filter = "*.json|*.json",
        };

        if (Show(dialog) == true)
        {
            ViewModel.Export(dialog.FileName);
        }
    }

    private void OnImportClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("Settings.Import"),
            Filter = "*.json|*.json",
            CheckFileExists = true,
        };

        if (Show(dialog) == true)
        {
            ViewModel.Import(dialog.FileName);
        }
    }

    /// <summary>
    /// Shows a file dialogue, and survives one that refuses to open: a failure
    /// here must not take the whole tray app down with it.
    /// </summary>
    private static bool? Show(FileDialog dialog)
    {
        try
        {
            return dialog.ShowDialog();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            Debug.WriteLine($"File dialogue failed: {exception.Message}");
            return null;
        }
    }

    private void OnOpenDataFolderClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenDataFolder();
    }
}