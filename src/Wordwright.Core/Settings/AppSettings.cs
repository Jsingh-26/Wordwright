namespace Wordwright.Core.Settings;

/// <summary>
/// The user's settings, persisted as <c>settings.json</c>
/// (docs/ARCHITECTURE.md → settings.json). <see cref="SchemaVersion"/> records
/// the file format; today only 1 exists, so there is no migration to do.
/// </summary>
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = SettingsStore.SchemaVersion;

    public bool SnippetsEnabled { get; init; } = true;

    public bool StartWithWindows { get; init; } = true;

    public bool CheckForAppUpdatesWeekly { get; init; }

    /// <summary>Processes (by exe file name) where Wordwright stays quiet.</summary>
    public IReadOnlyList<string> ExcludedApps { get; init; } =
        ["KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe"];

    /// <summary>"system", or a fixed theme name once the settings UI allows one.</summary>
    public string Theme { get; init; } = "system";

    /// <summary>Where the main window was when it last closed, so the next launch
    /// puts it back. Null until the window has been closed once.</summary>
    public WindowPlacement? Window { get; init; }
}