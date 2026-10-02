using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Wordwright.App.Resources;
using Wordwright.App.Snippets;
using Wordwright.App.ViewModels;

namespace Wordwright.App.Pages;

public partial class SnippetsPage : Page
{
    /// <summary>Set while a click or an arrow key is choosing a row, so the glide
    /// runs for the user's selection but not for programmatic ones
    /// (docs/PLAN.md → P11.5: not at window open, not after add or delete).</summary>
    private bool _userSelecting;

    public SnippetsPage()
    {
        InitializeComponent();

        ViewModel = new SnippetsViewModel((App)Application.Current);
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

        // Page entrance (docs/PLAN.md → P11.5).
        Motion.Enter(this);

        // The empty state's playground acknowledges its first expansion (P11.6).
        if (((App)Application.Current).SnippetEngine is { } engine)
        {
            engine.Expanded += OnSnippetExpanded;
        }

        // Quitting from the tray must not lose the last keystrokes (P12.4).
        ((App)Application.Current).FlushingEdits += OnFlushingEdits;
    }

    private void OnFlushingEdits(object? sender, EventArgs e) => ViewModel.Flush();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Flush();
        ((App)Application.Current).FlushingEdits -= OnFlushingEdits;

        if (((App)Application.Current).SnippetEngine is { } engine)
        {
            engine.Expanded -= OnSnippetExpanded;
        }
    }

    /// <summary>The completion moment: the first snippet expanded in the empty
    /// state's box reveals the done line, with the check-circle to its left,
    /// through the one entrance animation (docs/PLAN.md → P11.6).</summary>
    private void OnSnippetExpanded(object? sender, EventArgs e)
    {
        if (!TryHereBox.IsKeyboardFocusWithin)
        {
            return;
        }

        EmptyTryHereCheck.Visibility = Visibility.Visible;
        EmptyTryHereLabel.Text = Strings.Get("Welcome.TryHere.Done");
        Motion.Enter(EmptyTryHereRow);
    }

    /// <summary>Adds an empty snippet and puts the caret in its Name field.</summary>
    internal void NewSnippet()
    {
        var item = ViewModel.AddNew();

        // Let the new row enter once the list has realised it (docs/PLAN.md → P11.5).
        Dispatcher.BeginInvoke(
            () =>
            {
                if (SnippetList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement row)
                {
                    Motion.Enter(row);
                }
            },
            DispatcherPriority.Loaded);

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
            // Fade the row out first, and delete once it has gone (P11.5). With
            // animations off Motion.Exit just reports completion immediately.
            if (SnippetList.ItemContainerGenerator.ContainerFromItem(snippet) is FrameworkElement row)
            {
                Motion.Exit(row, () => ViewModel.DeleteSelected());
            }
            else
            {
                ViewModel.DeleteSelected();
            }
        }
    }

    /// <summary>The "Saved" line fades in and out as the view model raises it,
    /// instead of snapping (docs/PLAN.md → P11.5).</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SnippetsViewModel.ShowSaved))
        {
            return;
        }

        if (ViewModel.ShowSaved)
        {
            Motion.Enter(SavedText);
        }
        else
        {
            Motion.Exit(SavedText);
        }
    }

    private void OnListPreviewMouseDown(object sender, MouseButtonEventArgs e) => _userSelecting = true;

    private void OnListPreviewMouseUp(object sender, MouseButtonEventArgs e) => _userSelecting = false;

    private void OnListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            _userSelecting = true;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var userInitiated = _userSelecting;
        _userSelecting = false;

        if (!userInitiated || !IsLoaded || ViewModel.SelectedSnippet is not { } item)
        {
            return;
        }

        GlideChipToEditor(item);
    }

    /// <summary>The D7 signature move: glide a proxy of the chosen row's shortcut
    /// chip into the editor's Shortcut field, then settle the editor groups
    /// (docs/PLAN.md → P11.5). A no-op when animations are off, and skipped when
    /// the row is virtualised off-screen, in which case only the settle runs.</summary>
    private void GlideChipToEditor(SnippetListItem item)
    {
        if (!Motion.Enabled)
        {
            return;
        }

        if (SnippetList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem row
            || row.Descendants().OfType<Border>().FirstOrDefault(border => Equals(border.Tag, "ShortcutChip"))
                is not { Child: System.Windows.Controls.TextBlock chipText } chip)
        {
            Motion.Settle(NameGroup, ShortcutGroup, BodyGroup);
            return;
        }

        var from = chip.TransformToVisual(this).TransformBounds(new Rect(chip.RenderSize));
        var to = ShortcutField.TransformToVisual(this).TransformBounds(new Rect(ShortcutField.RenderSize));

        var proxy = new Border
        {
            Width = from.Width,
            Height = from.Height,
            Background = chip.Background,
            CornerRadius = chip.CornerRadius,
            Padding = chip.Padding,
            Child = new System.Windows.Controls.TextBlock
            {
                Text = chipText.Text,
                FontFamily = chipText.FontFamily,
                FontSize = chipText.FontSize,
                Foreground = chipText.Foreground,
            },
        };

        // Decorative: it is a plain Border with no name, so a screen reader has
        // nothing to read from it (docs/PLAN.md → P11.5), and it lives on a
        // hit-test-invisible canvas.
        Canvas.SetLeft(proxy, from.X);
        Canvas.SetTop(proxy, from.Y);
        Overlay.Children.Add(proxy);

        // Dim the field so the chip reads as landing in it, then light it back up.
        ShortcutField.Opacity = 0.5;

        Motion.Glide(proxy, from, to, () =>
        {
            Motion.Exit(proxy, () => Overlay.Children.Remove(proxy));
            Motion.Reveal(ShortcutField, 0.5);
            Motion.Settle(NameGroup, ShortcutGroup, BodyGroup);
        });
    }
}