using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.Core.Actions;

namespace Wordwright.App.Palette;

/// <summary>The palette's filtering and selection state (docs/DESIGN.md §6).</summary>
internal sealed partial class PaletteViewModel : ObservableObject
{
    private readonly List<AiAction> _all;

    public PaletteViewModel(IReadOnlyList<AiAction> actions)
    {
        _all = [.. actions.Where(action => action.Enabled)];
        Actions = new ObservableCollection<AiAction>(_all);
        SelectedAction = Actions.FirstOrDefault();
    }

    internal IReadOnlyList<AiAction> AllActions => _all;

    public ObservableCollection<AiAction> Actions { get; }

    [ObservableProperty]
    private AiAction? _selectedAction;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string _customInstruction = string.Empty;

    partial void OnFilterChanged(string value) => ApplyFilter(value);

    private void ApplyFilter(string query)
    {
        var wanted = query.Trim().Length == 0
            ? _all
            : _all.Where(action =>
                action.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || action.ShortcutKey.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                || (action.Hotkey?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
              .ToList();

        Actions.Clear();
        foreach (var action in wanted)
        {
            Actions.Add(action);
        }

        SelectedAction = Actions.FirstOrDefault();
    }

    /// <summary>Moves the selection by <paramref name="delta"/> rows.</summary>
    public void MoveSelection(int delta)
    {
        if (Actions.Count == 0)
        {
            return;
        }

        var index = SelectedAction is null ? 0 : Actions.IndexOf(SelectedAction);
        index = Math.Clamp(index + delta, 0, Actions.Count - 1);
        SelectedAction = Actions[index];
    }

    /// <summary>Picks the action whose letter matches a single typed letter.</summary>
    public void FilterByLetter(string letter)
    {
        var match = _all.FirstOrDefault(action =>
            string.Equals(action.ShortcutKey, letter, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            SelectedAction = match;
        }
    }
}
