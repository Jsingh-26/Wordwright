namespace Wordwright.Core.Hardware;

/// <summary>
/// Picks the hardware tier: the first rule in docs/MODELS.md → Hardware tiers
/// that matches, with the GPU winning because it is by far the fastest.
/// </summary>
public static class TierClassifier
{
    /// <summary>
    /// A gigabyte as Windows, the shops and the model catalog mean it — a
    /// decimal one. Physical memory is reported a few hundred megabytes short of
    /// the installed figure once firmware takes its share, so a 16 GB PC reports
    /// about 17 GB and must not be pushed down a tier by the difference.
    /// </summary>
    private const ulong Gigabyte = 1_000_000_000;

    private const ulong GpuMemoryFloor = 6 * Gigabyte;

    private const ulong Cpu16Floor = 16 * Gigabyte;

    private const ulong Cpu8Floor = 8 * Gigabyte;

    public static HardwareTier Classify(HardwareProfile profile)
    {
        if (profile.Gpus.Any(gpu => gpu.DedicatedMemoryBytes >= GpuMemoryFloor))
        {
            return HardwareTier.Gpu;
        }

        // Both CPU tiers need AVX2; without it the model would be unusably slow.
        if (!profile.HasAvx2)
        {
            return HardwareTier.Minimal;
        }

        if (profile.TotalRamBytes >= Cpu16Floor)
        {
            return HardwareTier.Cpu16;
        }

        return profile.TotalRamBytes >= Cpu8Floor ? HardwareTier.Cpu8 : HardwareTier.Minimal;
    }
}
