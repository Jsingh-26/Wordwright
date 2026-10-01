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

        Loaded += OnLoaded;
    }

    internal SnippetsViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The prefix lives on the Settings page, so read the library again
        // whenever this page comes back into view.
        ViewModel.Reload();

        // The window's accelerators (Ctrl+N, Ctrl+F, Delete) need to find the page
        // that is on screen (docs/PLAN.md → P3.4a).
        (Window.GetWindow(this) as MainWindow)?.OnSnippetsPageLoaded(this);
    }

    /// <summary>Adds an empty snippet and puts the caret in its Name field.</summary>
    internal void NewSnippet()
    {
        ViewModel.AddNew();

        // A new snippet has no shortcut yet, so start the user at its name.
        NameBox.Focus();
    }

    /// <summary>The window's Ctrl+F.</summary>
    internal void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnNewSnippetClicked(object sender, RoutedEventArgs e) => NewSnippet();

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

    private void OnDeleteSnippetClicked(object sender, RoutedEventArgs e) => ConfirmDeleteSelected();

    /// <summary>Asks before deleting the selected snippet; the window's Delete key
    /// and the page's own button both come here.</summary>
    internal async void ConfirmDeleteSelected()
    {
        if (ViewModel.SelectedSnippet is not { } snippet)
        {
            return;
        }

        // The window owns the one dialog host WPF-UI allows per window
        // (MainWindow.xaml); the page is built afresh on later visits.
        var host = ContentDialogHost.GetForWindow(Window.GetWindow(this)!);

        var dialog = new ContentDialog(host)
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