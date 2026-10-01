using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Wordwright.Core.Actions;
using Wordwright.Core.Snippets;
using Wordwright.Platform.Input;

namespace Wordwright.App.Palette;

/// <summary>What the palette offers (docs/DESIGN.md §6, docs/PLAN.md P6.8).</summary>
public enum PaletteMode
{
    /// <summary>AI is on: the actions, then the snippet group.</summary>
    Actions,

    /// <summary>AI is off but text is selected: the invitation to turn it on.</summary>
    AiOff,

    /// <summary>AI is off and nothing is selected: the snippet group alone.</summary>
    SnippetsOnly,
}

/// <summary>
/// The action palette (docs/DESIGN.md §6, docs/PLAN.md P6.5 and P6.8): a small
/// floating window near the caret listing every enabled action, then a "Snippets"
/// group listing every enabled snippet. Type to filter, arrows and Enter to pick,
/// a letter to jump to its action, Esc to close. Picking a snippet inserts it at
/// the caret through the normal snippet path; picking an action starts a rewrite.
/// </summary>
public partial class PaletteWindow : FluentWindow
{
    /// <summary>The action the user picked, or null when they picked a snippet or
    /// closed the palette.</summary>
    public AiAction? ChosenAction { get; private set; }

    /// <summary>The snippet the user picked, or null.</summary>
    public Snippet? ChosenSnippet { get; private set; }

    /// <summary>What the user typed as a custom instruction, when they chose
    /// the custom action.</summary>
    public string? CustomInstruction { get; private set; }

    /// <summary>The text captured before the palette opened.</summary>
    public string? Selection { get; set; }

    /// <summary>What the palette is for.</summary>
    public PaletteMode Mode { get; init; } = PaletteMode.Actions;

    /// <summary>True when the palette should invite the user to turn AI on.</summary>
    public bool ShowAiOff => Mode == PaletteMode.AiOff;

    /// <summary>Raised once the user has chosen an action or a snippet.</summary>
    public event EventHandler? Chosen;

    /// <summary>The user asked to turn offline AI on from the palette.</summary>
    public event EventHandler? TurnOnAiRequested;

    internal PaletteViewModel ViewModel { get; }

    private bool _choosing;

    public PaletteWindow(
        IReadOnlyList<AiAction> actions,
        IReadOnlyList<Snippet> snippets,
        string prefix,
        PaletteMode mode = PaletteMode.Actions)
    {
        Mode = mode;
        ViewModel = new PaletteViewModel(actions, snippets, prefix, actionsEnabled: mode == PaletteMode.Actions);

        InitializeComponent();
        DataContext = ViewModel;

        AiOffPanel.Visibility = ShowAiOff ? Visibility.Visible : Visibility.Collapsed;
        ActionList.Visibility = ShowAiOff ? Visibility.Collapsed : Visibility.Visible;

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

    private void OnFilterKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnListKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnCustomKeyDown(object sender, KeyEventArgs e) => HandleKey(e);

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void OnTurnOnAiClicked(object sender, RoutedEventArgs e)
    {
        TurnOnAiRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

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
                ActionList.ScrollIntoView(ViewModel.SelectedRow);
                e.Handled = true;
                return;

            case Key.Up:
                ViewModel.MoveSelection(-1);
                ActionList.ScrollIntoView(ViewModel.SelectedRow);
                e.Handled = true;
                return;

            case Key.Enter:
                Choose();
                e.Handled = true;
                return;
        }

        // A bare letter jumps to the action with that letter (docs/DESIGN.md §6).
        // Letters typed with a modifier are left alone.
        if (e.Key is >= Key.A and <= Key.Z
            && Keyboard.Modifiers == ModifierKeys.None
            && !CustomPanel.IsVisible)
        {
            JumpToLetter(e.Key.ToString());
            e.Handled = true;
        }
    }

    private void JumpToLetter(string letter)
    {
        var row = ViewModel.Rows.FirstOrDefault(candidate =>
            !candidate.IsHeader
            && string.Equals(candidate.Letter, letter, StringComparison.OrdinalIgnoreCase));

        if (row is not null)
        {
            ViewModel.SelectedRow = row;
            ActionList.ScrollIntoView(row);
        }
    }

    private void Choose()
    {
        if (ViewModel.SelectedRow is not { IsHeader: false } row)
        {
            return;
        }

        if (row.IsSnippet)
        {
            _choosing = true;
            ChosenSnippet = row.Snippet;
            Chosen?.Invoke(this, EventArgs.Empty);
            Close();
            return;
        }

        if (row.Action is not { } action)
        {
            return;
        }

        if (action.Id == "custom" && CustomBox.Text.Trim().Length == 0)
        {
            // Reveal the instruction box rather than run an empty instruction.
            CustomPanel.Visibility = Visibility.Visible;
            CustomBox.Focus();
            return;
        }

        _choosing = true;
        ChosenAction = action;

        if (action.Id == "custom")
        {
            CustomInstruction = CustomBox.Text.Trim();
        }

        Chosen?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
