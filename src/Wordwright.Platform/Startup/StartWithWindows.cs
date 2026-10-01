using System.Diagnostics;
using Microsoft.Win32;
using Wordwright.Platform.Packaging;

namespace Wordwright.Platform.Startup;

/// <summary>
/// Keeps "Start Wordwright when I sign in" in sync with the user's
/// <c>startWithWindows</c> setting (docs/AGENTS.md P1.5).
/// <para>
/// The installer build uses the
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> entry — HKCU only:
/// no admin rights, and it affects just this user. The Microsoft Store package
/// cannot use it (Store policy, docs/PLAN.md P3.6), so there the task declared
/// in the package manifest does the same job.
/// </para>
/// </summary>
public static class StartWithWindows
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wordwright";

    /// <summary>The startup task's id, which must match the manifest's
    /// <c>desktop:StartupTask TaskId</c>.</summary>
    private const string TaskId = "Wordwright";

    public static void Apply(bool enabled, string exePath)
    {
        if (PackageIdentity.IsPackaged)
        {
            // Windows owns this state: the user can switch the task off in Task
            // Manager too, and the call simply reports that instead of changing
            // it, so the app never fights the user over it.
            _ = ApplyStartupTaskAsync(enabled);
            return;
        }

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

    private static async Task ApplyStartupTaskAsync(bool enabled)
    {
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);

            if (enabled)
            {
                var state = await task.RequestEnableAsync();
                if (state != Windows.ApplicationModel.StartupTaskState.Enabled)
                {
                    Debug.WriteLine($"Startup task stayed {state}");
                }
            }
            else
            {
                task.Disable();
            }
        }
        catch (Exception exception)
        {
            // A refusal here must not take the app down on start-up.
            Debug.WriteLine($"Startup task failed: {exception}");
        }
    }
}
