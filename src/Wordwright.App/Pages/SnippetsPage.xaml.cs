using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Wordwright.App.Resources;
using Wordwright.App.ViewModels;

namespace Wordwright.App.Pages;

public partial class SnippetsPage : Page
{
    public SnippetsPage()
    {
        InitializeComponent();

        ViewModel = new SnippetsViewModel((App)Application.Current);
        DataContext = ViewModel;
    }

    internal SnippetsViewModel ViewModel { get; }

    private void OnNewSnippetClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddNew();

        // A new snippet has no shortcut yet, so start the user at its name.
        NameBox.Focus();
    }

    private async void OnDeleteSnippetClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSnippet is not { } snippet)
        {
            return;
        }

        var dialog = new ContentDialog(DialogHost)
        {
            Content = Strings.Get("Snippets.DeleteConfirm", ("Name", snippet.Name)),
            PrimaryButtonText = Strings.Get("Snippets.Delete"),
            CloseButtonText = Strings.Get("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.DeleteSelected();
        }
    }
}