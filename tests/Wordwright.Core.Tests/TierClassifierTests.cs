using FluentAssertions;
using Wordwright.Core.Hardware;

namespace Wordwright.Core.Tests;

public class TierClassifierTests
{
    private const ulong Gigabyte = 1_000_000_000;

    /// <summary>A PC with nothing special about it unless a test says so.</summary>
    private static HardwareProfile Profile(
        ulong ram = 8 * Gigabyte,
        ulong available = 8 * Gigabyte,
        bool avx2 = true,
        ulong vram = 0,
        long disk = 100 * (long)Gigabyte) => new()
        {
            TotalRamBytes = ram,
            AvailableRamBytes = available,
            CpuName = "Test CPU",
            PhysicalCores = 4,
            HasAvx2 = avx2,
            HasAvx512 = false,
            Gpus = vram > 0 ? [new GpuInfo("Test GPU", vram)] : [],
            FreeDiskBytes = disk,
            OsBuild = "10.0.26100",
        };

    [Fact]
    public void AGpuWithSixGigabytes_wins()
    {
        var profile = Profile(ram: 64 * Gigabyte, vram: 6 * Gigabyte);

        TierClassifier.Classify(profile).Should().Be(HardwareTier.Gpu);
    }

    [Fact]
    public void AGpuWithLessThanSixGigabytes_isNotEnough()
    {
        // A 4 GB card still leaves the model to the CPU.
        var profile = Profile(ram: 16 * Gigabyte, vram: 4 * Gigabyte);

        TierClassifier.Classify(profile).Should().Be(HardwareTier.Cpu16);
    }

    [Fact]
    public void TheBestGpuDecides_notTheFirstOne()
    {
        var profile = Profile(ram: 32 * Gigabyte, vram: 2 * Gigabyte) with
        {
            Gpus = [new GpuInfo("Onboard", 512_000_000), new GpuInfo("Discrete", 8 * Gigabyte)],
        };

        TierClassifier.Classify(profile).Should().Be(HardwareTier.Gpu);
    }

    [Fact]
    public void SixteenGigabytesAndAvx2_isTheMiddleTier()
    {
        TierClassifier.Classify(Profile(ram: 16 * Gigabyte)).Should().Be(HardwareTier.Cpu16);
    }

    [Fact]
    public void AGigabyteShortOfSixteen_isTheLowerTier()
    {
        // 15 GB is not 16 GB, however close the marketing makes it sound.
        TierClassifier.Classify(Profile(ram: 16 * Gigabyte - 1)).Should().Be(HardwareTier.Cpu8);
    }

    [Fact]
    public void EightGigabytesAndAvx2_isTheLowestModelTier()
    {
        TierClassifier.Classify(Profile(ram: 8 * Gigabyte)).Should().Be(HardwareTier.Cpu8);
    }

    [Fact]
    public void JustUnderEightGigabytes_isMinimal()
    {
        TierClassifier.Classify(Profile(ram: 8 * Gigabyte - 1)).Should().Be(HardwareTier.Minimal);
    }

    [Fact]
    public void APcAsWindowsReportsIt_isStillSixteenGigabytes()
    {
        // 16 GB of memory reports as about 17 GB once firmware has taken its
        // share; the tier must not depend on that difference.
        TierClassifier.Classify(Profile(ram: 17_053_000_000)).Should().Be(HardwareTier.Cpu16);
    }

    [Fact]
    public void WithoutAvx2_evenALotOfMemoryIsMinimal()
    {
        TierClassifier.Classify(Profile(ram: 64 * Gigabyte, avx2: false)).Should().Be(HardwareTier.Minimal);
    }

    [Fact]
    public void AGpuPCNeedsNoAvx2_becauseTheModelRunsOnTheGpu()
    {
        var profile = Profile(ram: 4 * Gigabyte, avx2: false, vram: 8 * Gigabyte);

        TierClassifier.Classify(profile).Should().Be(HardwareTier.Gpu);
    }

    [Fact]
    public void TierNames_matchTheCatalog()
    {
        HardwareTier.Gpu.ToCatalogName().Should().Be("gpu");
        HardwareTier.Cpu16.ToCatalogName().Should().Be("cpu16");
        HardwareTier.Cpu8.ToCatalogName().Should().Be("cpu8");
        HardwareTier.Minimal.ToCatalogName().Should().Be("minimal");
    }
}
