using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.App.Resources;
using Wordwright.Core.Actions;
using Wordwright.Core.Snippets;

namespace Wordwright.App.Palette;

/// <summary>What a palette row is.</summary>
internal enum PaletteRowKind
{
    /// <summary>The "Snippets" group heading.</summary>
    Header,

    /// <summary>One AI action.</summary>
    Action,

    /// <summary>One snippet to insert.</summary>
    Snippet,
}

/// <summary>One row in the palette: an action, a snippet, or a group heading.</summary>
internal sealed class PaletteRow
{
    public required PaletteRowKind Kind { get; init; }

    public required string Name { get; init; }

    /// <summary>The letter column (actions only).</summary>
    public string Letter { get; init; } = "";

    /// <summary>The right column: an action's hotkey, or a snippet's shortcut.</summary>
    public string Detail { get; init; } = "";

    public AiAction? Action { get; init; }

    public Snippet? Snippet { get; init; }

    public bool IsHeader => Kind == PaletteRowKind.Header;

    public bool IsSnippet => Kind == PaletteRowKind.Snippet;
}

/// <summary>
/// The palette's contents (docs/DESIGN.md §6): every enabled action, then a
/// "Snippets" group listing every enabled snippet. The filter matches action
/// names, letters and hotkeys, and snippet names and shortcuts; the letter shown
/// beside an action is the mnemonic you type to bring it to the top.
/// </summary>
internal sealed partial class PaletteViewModel : ObservableObject
{
    private readonly List<AiAction> _actions;
    private readonly List<(Snippet Snippet, string Shortcut)> _snippets;
    private readonly bool _actionsEnabled;

    public PaletteViewModel(
        IReadOnlyList<AiAction> actions,
        IReadOnlyList<Snippet> snippets,
        string prefix,
        bool actionsEnabled)
    {
        _actions = [.. actions.Where(action => action.Enabled)];
        _snippets = [.. snippets.Select(snippet => (snippet, prefix + snippet.Trigger))];
        _actionsEnabled = actionsEnabled;

        Rebuild();
    }

    public ObservableCollection<PaletteRow> Rows { get; } = [];

    [ObservableProperty]
    private PaletteRow? _selectedRow;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string _customInstruction = string.Empty;

    /// <summary>True when the palette is showing snippets only and none match.</summary>
    [ObservableProperty]
    private bool _noSnippetsMatch;

    /// <summary>Whether the "Snippets" heading is shown.</summary>
    [ObservableProperty]
    private bool _showSnippetsHeader;

    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var query = Filter.Trim();

        var actions = _actionsEnabled
            ? _actions.Where(action => Matches(action, query)).ToList()
            : [];

        var snippets = _snippets
            .Where(entry => Matches(entry.Snippet, entry.Shortcut, query))
            .ToList();

        Rows.Clear();

        foreach (var action in actions)
        {
            Rows.Add(new PaletteRow
            {
                Kind = PaletteRowKind.Action,
                Name = action.Name,
                Letter = action.ShortcutKey,
                Detail = action.Hotkey ?? "",
                Action = action,
            });
        }

        ShowSnippetsHeader = snippets.Count > 0;
        if (snippets.Count > 0)
        {
            Rows.Add(new PaletteRow
            {
                Kind = PaletteRowKind.Header,
                Name = Strings.Get("Palette.Snippets"),
            });

            foreach (var (snippet, shortcut) in snippets)
            {
                Rows.Add(new PaletteRow
                {
                    Kind = PaletteRowKind.Snippet,
                    Name = snippet.Name.Length > 0 ? snippet.Name : shortcut,
                    Detail = shortcut,
                    Snippet = snippet,
                });
            }
        }

        // The snippet-only palette (AI off, nothing selected) says so when a
        // filter matches nothing (docs/UX_COPY.md → Palette.Snippets.Empty).
        NoSnippetsMatch = !_actionsEnabled && snippets.Count == 0 && query.Length > 0;

        SelectedRow = Rows.FirstOrDefault(row => !row.IsHeader);
    }

    private static bool Matches(AiAction action, string query) =>
        query.Length == 0
        || action.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || action.ShortcutKey.StartsWith(query, StringComparison.OrdinalIgnoreCase)
        || (action.Hotkey?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool Matches(Snippet snippet, string shortcut, string query) =>
        query.Length == 0
        || snippet.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || shortcut.Contains(query, StringComparison.OrdinalIgnoreCase)
        || snippet.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>Moves the selection by <paramref name="delta"/> selectable rows.</summary>
    public void MoveSelection(int delta)
    {
        var selectable = Rows.Where(row => !row.IsHeader).ToList();
        if (selectable.Count == 0)
        {
            SelectedRow = null;
            return;
        }

        var index = SelectedRow is null ? -1 : selectable.IndexOf(SelectedRow);
        index = Math.Clamp(index + delta, 0, selectable.Count - 1);
        SelectedRow = selectable[index];
    }
}
