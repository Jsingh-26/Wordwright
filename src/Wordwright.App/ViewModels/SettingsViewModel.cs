using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wordwright.App.Resources;
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
        SnippetPrefix = app.Snippets.TriggerPrefix;
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
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    /// <summary>The result of the last import, shown under the buttons.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool HasExcludedApps => ExcludedApps.Count > 0;

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