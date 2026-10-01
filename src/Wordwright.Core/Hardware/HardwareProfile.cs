namespace Wordwright.Core.Hardware;

/// <summary>
/// What the PC has, as <c>Wordwright.Platform.HardwareProbe</c> reads it
/// (docs/ARCHITECTURE.md → Hardware probing). It lives in Core, with the tier
/// rules that read it, so those stay unit-testable without WMI or a GPU.
/// </summary>
public sealed record HardwareProfile
{
    public required ulong TotalRamBytes { get; init; }

    public required ulong AvailableRamBytes { get; init; }

    public required string CpuName { get; init; }

    public required int PhysicalCores { get; init; }

    public required bool HasAvx2 { get; init; }

    public required bool HasAvx512 { get; init; }

    /// <summary>Every adapter with dedicated memory; empty on a PC with none.</summary>
    public required IReadOnlyList<GpuInfo> Gpus { get; init; }

    public required long FreeDiskBytes { get; init; }

    public required string OsBuild { get; init; }
}

/// <summary>A graphics adapter and the memory only it can use.</summary>
public sealed record GpuInfo(string Name, ulong DedicatedMemoryBytes);
