using Microsoft.Win32;

namespace Wordwright.App.Tray;

/// <summary>
/// Reads the Windows system theme (which the taskbar follows) from the
/// Personalize registry key, so the tray glyph can match the taskbar.
/// </summary>
internal static class SystemTheme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>True when the taskbar is light; false when it is dark. Defaults to light.</summary>
    internal static bool TaskbarUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("SystemUsesLightTheme") is not int value || value != 0;
    }
}