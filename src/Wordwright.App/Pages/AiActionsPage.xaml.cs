using System.Windows;
using System.Windows.Controls;
using Wordwright.App.ViewModels;

namespace Wordwright.App.Pages;

public partial class AiActionsPage : Page
{
    public AiActionsPage()
    {
        InitializeComponent();

        ViewModel = new AiActionsViewModel((App)Application.Current);
        DataContext = ViewModel;

        // The recorder asks the view model about clashes with the palette hotkey
        // and the other actions (docs/UX_COPY.md → Actions.Hotkey.*).
        HotkeyBox.Validate = ViewModel.ValidateHotkey;

        // The AI-on state may have changed while another page was showing, so
        // ask again whenever this page appears.
        Loaded += (_, _) => ViewModel.RefreshAiState();
    }

    internal AiActionsViewModel ViewModel { get; }

    private void OnNewActionClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddNew();
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private async void OnTryItClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.TryItAsync();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e) => ViewModel.ResetSelected();

    private void OnClearHotkeyClicked(object sender, RoutedEventArgs e) => ViewModel.Hotkey = null;
}
