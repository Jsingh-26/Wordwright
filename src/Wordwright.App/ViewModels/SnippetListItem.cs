using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.Core.Snippets;

namespace Wordwright.App.ViewModels;

/// <summary>One row in the Snippets page list: the shortcut the user types and
/// the snippet's name (docs/DESIGN.md §2).</summary>
internal sealed partial class SnippetListItem : ObservableObject
{
    private readonly string _prefix;

    public SnippetListItem(string prefix, Snippet snippet)
    {
        _prefix = prefix;
        _snippet = snippet;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shortcut))]
    [NotifyPropertyChangedFor(nameof(Name))]
    private Snippet _snippet;

    /// <summary>Ready to read: prefix and trigger together, e.g. <c>;sig</c>.</summary>
    public string Shortcut => _prefix + Snippet.Trigger;

    public string Name => Snippet.Name;

    public string Id => Snippet.Id;
}