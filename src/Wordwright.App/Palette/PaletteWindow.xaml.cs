using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Wordwright.Core.Actions;
using Wordwright.Platform.Input;

namespace Wordwright.App.Palette;

/// <summary>
/// The action palette (docs/DESIGN.md §6, docs/PLAN.md P6.5): a small floating
/// window near the caret that lists every enabled action, filters as the user
/// types, and runs the chosen one. It never takes focus from the app being
/// rewritten — but it does take keyboard focus, because the user types into it,
/// and closing it returns focus to where it was.
/// <para>
/// This window raises intent only. Running the rewrite, pasting and the pill are
/// P6.6; the snippet group is P6.8.
/// </para>
/// </summary>
public partial class PaletteWindow : FluentWindow
{
    /// <summary>Which action the user picked, or null when the palette was closed
    /// without a choice.</summary>
    public AiAction? Chosen { get; private set; }

    /// <summary>What the user typed as a custom instruction, when they chose
    /// <c>custom</c>.</summary>
    public string? CustomInstruction { get; private set; }

    /// <summary>Raised after <see cref="Chosen"/> is set, once the window is
    /// closing. P6.6 hangs the rewrite off this.</summary>
    public event EventHandler? ChosenAction;

    /// <summary>True when the palette should invite the user to turn AI on,
    /// because AI is off and there is a selection.</summary>
    public bool ShowAiOff { get; init; }

    internal PaletteViewModel ViewModel { get; }

    public PaletteWindow(IReadOnlyList<AiAction> actions, bool showAiOff = false)
    {
        ViewModel = new PaletteViewModel(actions);
        ShowAiOff = showAiOff;

        InitializeComponent();
        DataContext = ViewModel;

        AiOffPanel.Visibility = showAiOff ? Visibility.Visible : Visibility.Collapsed;
        ActionList.Visibility = showAiOff ? Visibility.Collapsed : Visibility.Visible;

        // Place near the caret, then focus the filter box so typing filters.
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var caret = CaretPosition.Current();
        Left = caret.X;
        Top = caret.Y + 20;

        FilterBox.Focus();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        // Clicking another app dismisses the palette the way Esc does.
        base.OnDeactivated(e);

        if (!_choosing)
        {
            Close();
        }
    }

    private bool _choosing;

    private void OnFilterKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnListKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnCustomKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void OnTurnOnAiClicked(object sender, RoutedEventArgs e)
    {
        // The window that wants to show the consent dialogue listens for this.
        TurnOnAiRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    /// <summary>The user asked to turn offline AI on from the palette.</summary>
    public event EventHandler? TurnOnAiRequested;

    private void HandleKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                return;

            case Key.Down:
                ViewModel.MoveSelection(1);
                ActionList.ScrollIntoView(ViewModel.SelectedAction);
                e.Handled = true;
                return;

            case Key.Up:
                ViewModel.MoveSelection(-1);
                ActionList.ScrollIntoView(ViewModel.SelectedAction);
                e.Handled = true;
                return;

            case Key.Enter:
                Choose();
                e.Handled = true;
                return;
        }

        // A bare letter picks the action with that letter, as the palette shows
        // (docs/DESIGN.md §6). Letters typed with a modifier are left alone.
        if (e.Key is >= Key.A and <= Key.Z
            && Keyboard.Modifiers == ModifierKeys.None
            && !CustomPanel.IsVisible)
        {
            ViewModel.FilterByLetter(e.Key.ToString());
            e.Handled = true;
        }
    }

    private void Choose()
    {
        if (ViewModel.SelectedAction is not { } item)
        {
            return;
        }

        if (item.Id == "custom" && CustomInstruction is null or "")
        {
            // Reveal the instruction box rather than run an empty instruction.
            ShowCustomBox();
            return;
        }

        _choosing = true;
        Chosen = item;

        if (item.Id == "custom")
        {
            CustomInstruction = CustomBox.Text.Trim();
        }

        ChosenAction?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ShowCustomBox()
    {
        CustomPanel.Visibility = Visibility.Visible;
        CustomBox.Focus();
    }

    internal IReadOnlyList<AiAction> AllActions => ViewModel.AllActions;
}
