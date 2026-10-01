using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.App.Resources;
using Wordwright.Core.Snippets;

namespace Wordwright.App.ViewModels;

/// <summary>
/// The Snippets page: a searchable list on the left and the editor on the right,
/// saving as the user types with a quiet "Saved" beside the page title
/// (docs/DESIGN.md §2). Validation follows docs/UX_COPY.md, and nothing is
/// written until the shortcut is one the matcher could use.
/// </summary>
internal sealed partial class SnippetsViewModel : ObservableObject
{
    /// <summary>How long typing pauses before the file is written.</summary>
    private const int SaveDelayMilliseconds = 600;

    private const int SavedIndicatorMilliseconds = 2000;

    private readonly App _app;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _savedTimer;
    private readonly List<SnippetListItem> _all = [];

    /// <summary>Builds the preview line. It never reads the clipboard: a
    /// <c>{clipboard}</c> variable shows the placeholder instead, so opening a
    /// snippet cannot disturb what the user copied (docs/PLAN.md → P3.4c).</summary>
    private readonly VariableExpander _previewExpander =
        new(() => DateTimeOffset.Now, () => Strings.Get("Snippets.Preview.Clipboard"));

    /// <summary>True while the editor is being filled from the list, so the
    /// change handlers stay quiet.</summary>
    private bool _loadingFields;

    public SnippetsViewModel(App app)
    {
        _app = app;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDelayMilliseconds) };
        _saveTimer.Tick += (_, _) => Save();

        _savedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SavedIndicatorMilliseconds) };
        _savedTimer.Tick += (_, _) =>
        {
            _savedTimer.Stop();
            ShowSaved = false;
        };

        Reload();
    }

    public ObservableCollection<SnippetListItem> Snippets { get; } = [];

    /// <summary>What the user types before a shortcut; from snippets.json.</summary>
    public string Prefix => _app.Snippets.TriggerPrefix;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SnippetListItem? _selectedSnippet;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _shortcut = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortcutMessage))]
    private string? _shortcutMessage;

    [ObservableProperty]
    private bool _shortcutMessageIsError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBodyMessage))]
    private string? _bodyMessage;

    [ObservableProperty]
    private bool _showSaved;

    /// <summary>Whether to show the shortcut message under the field.</summary>
    public bool HasShortcutMessage => !string.IsNullOrEmpty(ShortcutMessage);

    /// <summary>Whether to show the body warning under the text field.</summary>
    public bool HasBodyMessage => !string.IsNullOrEmpty(BodyMessage);

    /// <summary>True when this PC has no snippets at all, for the empty state.</summary>
    public bool IsEmpty => _all.Count == 0;

    /// <summary>True when the editor has something to edit.</summary>
    public bool HasSelection => SelectedSnippet is not null;

    /// <summary>The help line under the shortcut field, with the real prefix.</summary>
    public string ShortcutHelp =>
        Strings.Get("Snippets.Field.ShortcutHelp", ("Prefix", Prefix), ("Shortcut", Shortcut));

    public string EmptyMessage => Strings.Get("Snippets.Empty", ("Prefix", Prefix));

    /// <summary>The playground label of the empty state (docs/PLAN.md → P3.4c).</summary>
    public string EmptyTryHere => Strings.Get("Snippets.Empty.TryHere", ("Prefix", Prefix));

    /// <summary>What the body would insert: its variables resolved, with
    /// <c>{cursor}</c> shown as the caret bar. Empty when there is nothing to
    /// resolve, which is when the preview line stays out of the way.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string _preview = string.Empty;

    public bool HasPreview => Preview.Length > 0;

    /// <summary>Starts a snippet and puts the editor on it.</summary>
    public SnippetListItem AddNew()
    {
        var now = DateTimeOffset.UtcNow;
        var snippet = new Snippet { Id = Snippet.NewId(), CreatedUtc = now, UpdatedUtc = now };

        var document = _app.Snippets with { Snippets = [.. _app.Snippets.Snippets, snippet] };
        _app.UpdateSnippets(document);

        Reload();
        SearchText = string.Empty;

        var item = _all.Single(entry => entry.Id == snippet.Id);
        SelectedSnippet = item;
        return item;
    }

    /// <summary>Deletes the selected snippet. The caller asks first.</summary>
    public void DeleteSelected()
    {
        if (SelectedSnippet is not { } item)
        {
            return;
        }

        _saveTimer.Stop();
        var document = _app.Snippets with
        {
            Snippets = _app.Snippets.Snippets.Where(snippet => snippet.Id != item.Id).ToList(),
        };

        _app.UpdateSnippets(document);
        Reload();

        SelectedSnippet = Snippets.Count > 0 ? Snippets[0] : null;
        ShowSaved = true;
        _savedTimer.Stop();
        _savedTimer.Start();
    }

    /// <summary>Reads the document again, e.g. after an import.</summary>
    public void Reload()
    {
        var selectedId = SelectedSnippet?.Id;

        _all.Clear();
        foreach (var snippet in _app.Snippets.Snippets)
        {
            _all.Add(new SnippetListItem(Prefix, snippet));
        }

        ApplyFilter();

        SelectedSnippet = selectedId is null
            ? Snippets.FirstOrDefault()
            : Snippets.FirstOrDefault(item => item.Id == selectedId) ?? Snippets.FirstOrDefault();

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(EmptyTryHere));
        OnPropertyChanged(nameof(Prefix));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedSnippetChanged(SnippetListItem? value)
    {
        _loadingFields = true;
        try
        {
            Name = value?.Snippet.Name ?? string.Empty;
            Shortcut = value?.Snippet.Trigger ?? string.Empty;
            Body = value?.Snippet.Body ?? string.Empty;
            ShortcutMessage = null;
            ShortcutMessageIsError = false;
            BodyMessage = null;
        }
        finally
        {
            _loadingFields = false;
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShortcutHelp));
    }

    partial void OnNameChanged(string value) => OnFieldChanged();

    partial void OnShortcutChanged(string value)
    {
        OnPropertyChanged(nameof(ShortcutHelp));
        OnFieldChanged();
    }

    partial void OnBodyChanged(string value)
    {
        // Outside the OnFieldChanged guard: the preview follows the body even
        // while the editor is being filled from the list.
        Preview = BuildPreview(value);

        OnFieldChanged();
    }

    /// <summary>Turns a body into its preview line, or nothing when it holds no
    /// variable to resolve.</summary>
    private string BuildPreview(string body)
    {
        if (!body.Contains('{'))
        {
            return string.Empty;
        }

        var expanded = _previewExpander.Expand(body);

        // The caret marker is shown as a bar, but only where the body had one:
        // CharactersAfterCursor is also zero when a marker sits at the very end.
        var preview = body.Contains(SnippetVariables.Cursor, StringComparison.Ordinal)
            ? expanded.Text.Insert(expanded.Text.Length - expanded.CharactersAfterCursor, "|")
            : expanded.Text;

        // Variables that resolved to exactly what was typed leave nothing to
        // show, so the line stays collapsed.
        return preview == body ? string.Empty : preview;
    }

    private void OnFieldChanged()
    {
        if (_loadingFields || SelectedSnippet is null)
        {
            return;
        }

        Validate();

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>The rules from docs/ARCHITECTURE.md, worded by docs/UX_COPY.md.</summary>
    private void Validate()
    {
        if (SnippetRules.IsValidTrigger(Shortcut))
        {
            var clash = _app.Snippets.Snippets.FirstOrDefault(snippet =>
                snippet.Id != SelectedSnippet?.Id
                && string.Equals(snippet.Trigger, Shortcut, StringComparison.Ordinal));

            ShortcutMessage = clash is null
                ? null
                : Strings.Get(
                    "Snippets.Error.ShortcutTaken",
                    ("Prefix", Prefix),
                    ("Shortcut", Shortcut),
                    ("Name", clash.Name));

            ShortcutMessageIsError = clash is not null;
        }
        else if (Shortcut.Length == 0)
        {
            // A snippet that has no shortcut yet is a work in progress, not an error.
            ShortcutMessage = null;
            ShortcutMessageIsError = false;
        }
        else
        {
            ShortcutMessage = Strings.Get("Snippets.Error.ShortcutInvalid");
            ShortcutMessageIsError = true;
        }

        BodyMessage = Body.Length > SnippetRules.LongBodyLength
            ? Strings.Get("Snippets.Warn.VeryLong")
            : null;
    }

    /// <summary>True when the shortcut is usable, so the snippet can be saved.</summary>
    private bool ShortcutIsUsable =>
        Shortcut.Length == 0
        || (SnippetRules.IsValidTrigger(Shortcut)
            && !SnippetRules.IsTriggerTaken(_app.Snippets.Snippets, Shortcut, SelectedSnippet?.Id));

    private void Save()
    {
        _saveTimer.Stop();

        if (SelectedSnippet is not { } item || !ShortcutIsUsable)
        {
            return;
        }

        // The trigger is stored without the prefix, so a stray prefix typed into
        // the shortcut field never reaches the file.
        var trigger = Shortcut.StartsWith(Prefix, StringComparison.Ordinal)
            ? Shortcut[Prefix.Length..]
            : Shortcut;

        var updated = item.Snippet with
        {
            Trigger = trigger,
            Name = Name,
            Body = Body,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        var document = _app.Snippets with
        {
            Snippets = _app.Snippets.Snippets
                .Select(snippet => snippet.Id == updated.Id ? updated : snippet)
                .ToList(),
        };

        _app.UpdateSnippets(document);
        item.Snippet = updated;

        ApplyFilter();

        ShowSaved = true;
        _savedTimer.Stop();
        _savedTimer.Start();
    }

    /// <summary>
    /// Brings the visible rows in line with the search text. It edits the
    /// collection in place rather than clearing it: clearing would drop the
    /// ListBox selection, which empties the editor mid-save.
    /// </summary>
    private void ApplyFilter()
    {
        var query = SearchText.Trim();

        var wanted = _all
            .Where(item =>
                query.Length == 0
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Snippet.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (var index = Snippets.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(Snippets[index]))
            {
                Snippets.RemoveAt(index);
            }
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            if (index >= Snippets.Count || !ReferenceEquals(Snippets[index], wanted[index]))
            {
                Snippets.Insert(index, wanted[index]);
            }
        }
    }
}