using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.App.Resources;
using Wordwright.Core.Actions;
using Wordwright.Core.Snippets;

namespace Wordwright.App.ViewModels;

/// <summary>
/// The Settings page (docs/PLAN.md P3.3): the snippet prefix, the apps where
/// Wordwright stays quiet, exporting and importing snippets, and the data
/// folder. Settings save as they change; importing adds to the library.
/// </summary>
internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly App _app;

    /// <summary>True while the fields are being filled, so change handlers stay quiet.</summary>
    private bool _loading;

    public SettingsViewModel(App app)
    {
        _app = app;

        _loading = true;
        StartWithWindows = app.Settings.StartWithWindows;
        AiEnabled = app.Settings.AiEnabled;
        SnippetPrefix = app.Snippets.TriggerPrefix;
        PaletteHotkey = app.Settings.PaletteHotkey;
        foreach (var executable in app.Settings.ExcludedApps)
        {
            ExcludedApps.Add(executable);
        }

        _loading = false;
    }

    public ObservableCollection<string> ExcludedApps { get; } = [];

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _snippetPrefix = "";

    [ObservableProperty]
    private string? _paletteHotkey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    /// <summary>The result of the last import, shown under the buttons.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool HasExcludedApps => ExcludedApps.Count > 0;

    /// <summary>
    /// Whether offline AI is on. Drives the Settings row that can turn it off.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOffAi))]
    private bool _aiEnabled;

    public bool CanTurnOffAi => AiEnabled;

    /// <summary>
    /// Turns offline AI off.
    ///
    /// This is the only model-management action that exists before P7.2 builds
    /// the Offline AI page, and it lives on Settings because that page is hidden
    /// whenever this PC cannot carry a model: someone who already had AI on must
    /// not be stranded with no way to switch it off. It cannot gain a model, so
    /// it does not bypass the eligibility rule (docs/PLAN.md → "Maintainer
    /// requirement: startup resource eligibility").
    /// </summary>
    public void TurnOffAi()
    {
        _app.UpdateSettings(_app.Settings with { AiEnabled = false });
        _app.Rewrite.Unload();
        AiEnabled = false;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
        {
            _app.UpdateSettings(_app.Settings with { StartWithWindows = value });
        }
    }

    partial void OnSnippetPrefixChanged(string value)
    {
        if (_loading || value.Trim() == _app.Snippets.TriggerPrefix)
        {
            return;
        }

        _app.UpdateSnippets(_app.Snippets with { TriggerPrefix = value.Trim() });
    }

    partial void OnPaletteHotkeyChanged(string? value)
    {
        if (_loading)
        {
            return;
        }

        _app.UpdateSettings(_app.Settings with { PaletteHotkey = value ?? "" });
    }

    /// <summary>Checks a palette-hotkey candidate for a clash with an action's own
    /// hotkey, for the recorder to show.</summary>
    public (bool Accepted, string? Message) ValidatePaletteHotkey(HotkeySpec candidate)
    {
        var text = candidate.ToString();

        if (HotkeyRules.Validate(text) != HotkeyValidation.Ok)
        {
            return (false, Strings.Get("Actions.Hotkey.Invalid"));
        }

        foreach (var action in _app.Actions.Actions.Where(action => action.Hotkey is not null))
        {
            if (HotkeySpec.Parse(action.Hotkey) is { } spec
                && spec.Modifiers == candidate.Modifiers
                && string.Equals(spec.Key, candidate.Key, StringComparison.Ordinal))
            {
                return (false, Strings.Get("Actions.Hotkey.Duplicate", ("Hotkey", text), ("Action", action.Name)));
            }
        }

        return (true, null);
    }

    public void AddExcludedApp(string executable)
    {
        var name = System.IO.Path.GetFileName(executable.Trim());

        if (name.Length == 0
            || ExcludedApps.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ExcludedApps.Add(name);
        SaveExcludedApps();
    }

    public void RemoveExcludedApp(string executable)
    {
        if (ExcludedApps.Remove(executable))
        {
            SaveExcludedApps();
        }
    }

    public void OpenDataFolder()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_app.UserDataFolder)
        {
            UseShellExecute = true,
        });
    }

    public void Export(string path)
    {
        SnippetExchange.Export(path, _app.Snippets);
        Message = null;
    }

    /// <summary>Adds the file's snippets to the library and reports what happened.</summary>
    public void Import(string path)
    {
        var imported = SnippetExchange.Import(path);
        if (imported is null)
        {
            Message = Strings.Get("Settings.ImportFailed");
            return;
        }

        var merge = SnippetExchange.Merge(_app.Snippets, imported);
        if (merge.Added > 0)
        {
            _app.UpdateSnippets(merge.Document);
        }

        Message = merge.Skipped == 0
            ? Strings.Get("Settings.ImportDone", ("Count", merge.Added))
            : $"{Strings.Get("Settings.ImportDone", ("Count", merge.Added))} " +
              Strings.Get("Settings.ImportSkipped", ("Count", merge.Skipped));

        OnPropertyChanged(nameof(HasMessage));
    }

    private void SaveExcludedApps()
    {
        OnPropertyChanged(nameof(HasExcludedApps));

        // The hook reads this list once, when it is built, so rebuild it.
        _app.UpdateSettings(_app.Settings with { ExcludedApps = [.. ExcludedApps] });
        _app.ApplyExcludedApps();
    }
}