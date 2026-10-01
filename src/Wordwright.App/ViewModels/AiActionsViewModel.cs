using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.App.Ai;
using Wordwright.App.Resources;
using Wordwright.Core.Actions;

namespace Wordwright.App.ViewModels;

/// <summary>
/// The AI actions page (docs/DESIGN.md §3): a list on the left and the editor on
/// the right, saving as the user types with a quiet "Saved". The six built-ins
/// can be edited like any other action and reset to their default.
/// </summary>
internal sealed partial class AiActionsViewModel : ObservableObject
{
    private const int SaveDelayMilliseconds = 600;

    private const int SavedIndicatorMilliseconds = 2000;

    private readonly App _app;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _savedTimer;
    private readonly List<ActionListItem> _all = [];

    /// <summary>True while the editor is being filled from the list.</summary>
    private bool _loadingFields;

    public AiActionsViewModel(App app)
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

    public ObservableCollection<ActionListItem> Actions { get; } = [];

    [ObservableProperty]
    private ActionListItem? _selectedAction;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _letter = string.Empty;

    [ObservableProperty]
    private string _instruction = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHotkeyMessage))]
    private string? _hotkeyMessage;

    [ObservableProperty]
    private string? _hotkey;

    public bool HasHotkeyMessage => !string.IsNullOrEmpty(HotkeyMessage);

    [ObservableProperty]
    private bool _showSaved;

    /// <summary>The text the "Try it" box holds; never persisted.</summary>
    [ObservableProperty]
    private string _tryItText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTryItMessage))]
    private string? _tryItMessage;

    [ObservableProperty]
    private bool _tryItBusy;

    public bool HasTryItMessage => !string.IsNullOrEmpty(TryItMessage);

    public bool HasSelection => SelectedAction is not null;

    /// <summary>Whether this action can be reset (docs/UX_COPY.md).</summary>
    public bool CanReset => SelectedAction?.Action.BuiltIn == true;

    /// <summary>Whether the "Try it" button does anything: AI must be on and a
    /// model installed, and there must be text to test with.</summary>
    public bool CanTryIt =>
        _app.Rewrite.ActiveModel() is not null
        && TryItText.Trim().Length > 0
        && SelectedAction?.Action.Instruction.Length > 0
        && !TryItBusy;

    /// <summary>The note under "Try it" when AI is off or nothing is installed.</summary>
    public string TryItHint => _app.Rewrite.ActiveModel() is null ? Strings.Get("Actions.NeedsAi") : string.Empty;

    public bool HasTryItHint => TryItHint.Length > 0;

    /// <summary>Starts an action and puts the editor on it.</summary>
    public ActionListItem AddNew()
    {
        var action = new AiAction
        {
            Id = AiAction.NewId(),
            Name = Strings.Get("Actions.New"),
            ShortcutKey = "",
        };

        var document = _app.Actions with { Actions = [.. _app.Actions.Actions, action] };
        _app.UpdateActions(document);

        Reload();
        var item = _all.Single(entry => entry.Id == action.Id);
        SelectedAction = item;
        return item;
    }

    /// <summary>Restores a built-in action and reloads the editor.</summary>
    public void ResetSelected()
    {
        if (SelectedAction is not { } item || !item.Action.BuiltIn)
        {
            return;
        }

        _app.ResetAction(item.Id);
        Reload();
        SelectedAction = _all.FirstOrDefault(entry => entry.Id == item.Id);
        ShowSaved = true;
        _savedTimer.Stop();
        _savedTimer.Start();
    }

    /// <summary>Reads the document again.</summary>
    public void Reload()
    {
        var selectedId = SelectedAction?.Id;

        _all.Clear();
        foreach (var action in _app.Actions.Actions)
        {
            _all.Add(new ActionListItem(action));
        }

        Actions.Clear();
        foreach (var item in _all)
        {
            Actions.Add(item);
        }

        SelectedAction = selectedId is null
            ? Actions.FirstOrDefault()
            : Actions.FirstOrDefault(item => item.Id == selectedId) ?? Actions.FirstOrDefault();

        OnPropertyChanged(nameof(CanTryIt));
        OnPropertyChanged(nameof(TryItHint));
        OnPropertyChanged(nameof(HasTryItHint));
    }

    partial void OnSelectedActionChanged(ActionListItem? value)
    {
        _loadingFields = true;
        try
        {
            Name = value?.Action.Name ?? string.Empty;
            Letter = value?.Action.ShortcutKey ?? string.Empty;
            Instruction = value?.Action.Instruction ?? string.Empty;
            Hotkey = value?.Action.Hotkey;
            HotkeyMessage = null;
            TryItMessage = null;
        }
        finally
        {
            _loadingFields = false;
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(CanTryIt));
    }

    partial void OnNameChanged(string value) => OnFieldChanged();

    partial void OnLetterChanged(string value) => OnFieldChanged();

    partial void OnInstructionChanged(string value)
    {
        OnPropertyChanged(nameof(CanTryIt));
        OnFieldChanged();
    }

    partial void OnHotkeyChanged(string? value)
    {
        if (!_loadingFields)
        {
            HotkeyMessage = null;
            OnFieldChanged();
        }
    }

    /// <summary>Checks a candidate hotkey for a clash, for the recorder to show.
    /// Returns whether it is accepted and the message when it is not.</summary>
    public (bool Accepted, string? Message) ValidateHotkey(HotkeySpec candidate)
    {
        var text = candidate.ToString();

        if (HotkeyRules.Validate(text) != HotkeyValidation.Ok)
        {
            return (false, Strings.Get("Actions.Hotkey.Invalid"));
        }

        // The palette hotkey and every other action's hotkey share one space.
        var taken = new List<(string Owner, HotkeySpec Spec)>();

        if (HotkeySpec.Parse(_app.Settings.PaletteHotkey) is { } palette)
        {
            taken.Add((Strings.Get("Settings.PaletteHotkey"), palette));
        }

        foreach (var action in _app.Actions.Actions.Where(action =>
                     action.Id != SelectedAction?.Id && action.Hotkey is not null))
        {
            if (HotkeySpec.Parse(action.Hotkey) is { } spec)
            {
                taken.Add((action.Name, spec));
            }
        }

        var clash = taken.FirstOrDefault(entry =>
            entry.Spec.Modifiers == candidate.Modifiers
            && string.Equals(entry.Spec.Key, candidate.Key, StringComparison.Ordinal));

        if (clash.Spec is not null)
        {
            return (false, Strings.Get("Actions.Hotkey.Duplicate", ("Hotkey", text), ("Action", clash.Owner)));
        }

        return (true, null);
    }

    partial void OnTryItTextChanged(string value) => OnPropertyChanged(nameof(CanTryIt));

    partial void OnTryItBusyChanged(bool value) => OnPropertyChanged(nameof(CanTryIt));

    /// <summary>Refreshes the AI-on state, e.g. when the page is shown again.</summary>
    public void RefreshAiState()
    {
        OnPropertyChanged(nameof(CanTryIt));
        OnPropertyChanged(nameof(TryItHint));
        OnPropertyChanged(nameof(HasTryItHint));
    }

    /// <summary>Runs the selected action over the "Try it" text, in place.</summary>
    public async Task TryItAsync()
    {
        if (SelectedAction is not { } item || !CanTryIt)
        {
            return;
        }

        TryItBusy = true;
        TryItMessage = null;

        try
        {
            var outcome = await _app.Rewrite.RewriteAsync(item.Action.Instruction, TryItText);

            if (outcome.Succeeded)
            {
                TryItText = outcome.Text;
            }

            TryItMessage = outcome.Status switch
            {
                RewriteStatus.TooLong => Strings.Get("Pill.TooLong"),
                RewriteStatus.BadOutput => Strings.Get("Pill.BadOutput"),
                RewriteStatus.NoModel => Strings.Get("Actions.NeedsAi"),
                _ => null,
            };
        }
        finally
        {
            TryItBusy = false;
        }
    }

    private void OnFieldChanged()
    {
        if (_loadingFields || SelectedAction is null)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save()
    {
        _saveTimer.Stop();

        if (SelectedAction is not { } item)
        {
            return;
        }

        var updated = item.Action with
        {
            Name = Name,
            ShortcutKey = Letter.Trim(),
            Instruction = Instruction,
            Hotkey = Hotkey,
        };

        var document = _app.Actions with
        {
            Actions = _app.Actions.Actions
                .Select(action => action.Id == updated.Id ? updated : action)
                .ToList(),
        };

        _app.UpdateActions(document);
        item.Action = updated;

        ShowSaved = true;
        _savedTimer.Stop();
        _savedTimer.Start();
    }
}
