using System.Globalization;
using System.Windows.Controls;
using Wordwright.App.Resources;
using Wordwright.Core.Hardware;
using Wordwright.Platform.Hardware;

namespace Wordwright.App.Pages;

/// <summary>
/// Shows what <see cref="HardwareProbe"/> read and which tier it earns
/// (docs/PLAN.md P4.1). Debug builds only: the nav item that opens it is added
/// under <c>#if DEBUG</c>, so a release build has no way to reach it.
/// </summary>
public partial class HardwarePage : Page
{
    public HardwarePage()
    {
        InitializeComponent();

        // WMI and DXGI take a moment; the page fills in when they answer.
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var profile = await Task.Run(HardwareProbe.Read);

        Values.ItemsSource = Rows(profile);
    }

    private static List<Row> Rows(HardwareProfile profile) =>
    [
        new(Strings.Get("Debug.Tier"), TierClassifier.Classify(profile).ToCatalogName()),
        new(Strings.Get("Debug.Cpu"), profile.CpuName),
        new(Strings.Get("Debug.Cores"), profile.PhysicalCores.ToString(CultureInfo.CurrentCulture)),
        new(Strings.Get("Debug.Memory"), Gigabytes(profile.TotalRamBytes)),
        new(Strings.Get("Debug.MemoryFree"), Gigabytes(profile.AvailableRamBytes)),
        new(Strings.Get("Debug.Avx2"), YesNo(profile.HasAvx2)),
        new(Strings.Get("Debug.Avx512"), YesNo(profile.HasAvx512)),
        new(Strings.Get("Debug.Gpus"), Gpus(profile)),
        new(Strings.Get("Debug.Disk"), Gigabytes((ulong)Math.Max(0, profile.FreeDiskBytes))),
        new(Strings.Get("Debug.OsBuild"), profile.OsBuild),
    ];

    private static string Gpus(HardwareProfile profile) =>
        profile.Gpus.Count == 0
            ? Strings.Get("Debug.No")
            : string.Join(", ", profile.Gpus.Select(gpu => $"{gpu.Name} ({Gigabytes(gpu.DedicatedMemoryBytes)})"));

    private static string YesNo(bool value) => Strings.Get(value ? "Debug.Yes" : "Debug.No");

    private static string Gigabytes(ulong bytes) =>
        (bytes / 1_000_000_000.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB";

    /// <summary>One line of the page.</summary>
    private sealed record Row(string Label, string Value);
}
