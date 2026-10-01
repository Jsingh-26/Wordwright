using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.Core.Actions;

namespace Wordwright.App.ViewModels;

/// <summary>One row in the AI actions list: the action's name and the letter
/// that picks it in the palette (docs/DESIGN.md §3).</summary>
internal sealed partial class ActionListItem : ObservableObject
{
    public ActionListItem(AiAction action) => _action = action;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(Letter))]
    private AiAction _action;

    public string Name => Action.Name;

    public string Letter => Action.ShortcutKey;

    public string Id => Action.Id;
}
