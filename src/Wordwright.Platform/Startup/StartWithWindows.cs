using Microsoft.Win32;

namespace Wordwright.Platform.Startup;

/// <summary>
/// Keeps the <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> entry
/// in sync with the user's <c>startWithWindows</c> setting. HKCU only: no
/// admin rights, and it affects just this user.
/// </summary>
public static class StartWithWindows
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wordwright";

    public static void Apply(bool enabled, string exePath)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            // Quoted: install paths can contain spaces.
            runKey.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}