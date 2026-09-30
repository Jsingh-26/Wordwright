using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

    /// <summary>Puts a variable where the caret is in the Text field and leaves
    /// the caret after it, so several can be inserted in a row.</summary>
    private void OnInsertClicked(object sender, RoutedEventArgs e)
    {
        // Qualified: Wpf.Ui.Controls.Button and System.Windows.Controls.Button
        // are both in scope here.
        if (sender is not System.Windows.Controls.Button { Tag: string variable })
        {
            return;
        }

        var start = BodyBox.SelectionStart;
        var selected = BodyBox.SelectionLength;
        var body = ViewModel.Body;

        ViewModel.Body = string.Concat(body.AsSpan(0, start), variable, body.AsSpan(start + selected));

        // Changing the text runs the caret to the end of the field, so put it back
        // after the token once the binding has finished pushing the new text.
        var caret = start + variable.Length;
        Dispatcher.BeginInvoke(
            () =>
            {
                BodyBox.Focus();
                BodyBox.CaretIndex = caret;
            },
            DispatcherPriority.Background);
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