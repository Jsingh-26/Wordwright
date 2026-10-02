using FluentAssertions;
using Wordwright.Core.Hardware;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

/// <summary>
/// docs/PLAN.md → "Maintainer requirement: startup resource eligibility"
/// (2026-10-02): offline AI is offered only while some model the catalog knows
/// about fits this PC's memory and disk, read as the machine is under normal
/// use. The user is never asked to close anything to qualify.
/// </summary>
public class AiEligibilityTests
{
    private const ulong Gigabyte = 1_000_000_000;

    private static HardwareProfile Profile(
        ulong available = 16 * Gigabyte,
        ulong ram = 16 * Gigabyte,
        ulong vram = 0,
        long disk = 100 * (long)Gigabyte) => new()
        {
            TotalRamBytes = ram,
            AvailableRamBytes = available,
            CpuName = "Test CPU",
            PhysicalCores = 8,
            HasAvx2 = true,
            HasAvx512 = false,
            Gpus = vram > 0 ? [new GpuInfo("Test GPU", vram)] : [],
            FreeDiskBytes = disk,
            OsBuild = "10.0.26100",
        };

    private static CatalogEntry Model(
        string id,
        string[] tiers,
        double ramGB = 2,
        long sizeBytes = 1 * (long)Gigabyte,
        string status = "candidate") => new()
        {
            Id = id,
            DisplayName = id,
            Tiers = tiers,
            RamRequiredGB = ramGB,
            SizeBytes = sizeBytes,
            Sha256 = "aa",
            Status = status,
            Speed = new Dictionary<string, CatalogSpeed>
            {
                [tiers[0]] = new CatalogSpeed { PromptTps = [60, 150], GenTps = [15, 30] },
            },
        };

    private static ModelCatalog Catalog(params CatalogEntry[] models) => new()
    {
        SchemaVersion = 1,
        CatalogVersion = "2026-10-02",
        Models = models,
    };

    [Fact]
    public void A_model_that_fits_makes_ai_available()
    {
        var result = AiEligibility.Check(Catalog(Model("tiny", ["cpu8"])), Profile());

        result.IsAvailable.Should().BeTrue();
        result.Reason.Should().Be(StepDownReason.None);
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("approved")]
    public void Approval_status_does_not_change_availability(string status)
    {
        // Availability is about what the PC can run. A capable PC whose catalog
        // has no approved model yet still gets the "no model ready to offer yet"
        // dialogue, not a hidden feature.
        var result = AiEligibility.Check(Catalog(Model("tiny", ["cpu8"], status: status)), Profile());

        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void The_sixteen_gigabyte_laptops_reading_blocks_ai()
    {
        // The reported case: about 2.01 GB free, and the Qwen candidate wants
        // 2.5 GB. Add the 1 GB headroom and nothing fits.
        var catalog = Catalog(Model("qwen3-0.6b-q8_0", ["cpu8", "cpu16"], ramGB: 2.5, sizeBytes: 639_446_688));

        var result = AiEligibility.Check(catalog, Profile(available: 2_010_000_000));

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(StepDownReason.NotEnoughRam);
    }

    [Fact]
    public void Too_little_disk_blocks_ai_even_when_memory_is_fine()
    {
        var catalog = Catalog(Model("tiny", ["cpu8"], ramGB: 2, sizeBytes: 1 * (long)Gigabyte));

        // 1 GB of file needs 1.2 GB free.
        var result = AiEligibility.Check(catalog, Profile(disk: 1_000_000_000));

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(StepDownReason.NotEnoughDisk);
    }

    [Fact]
    public void A_catalog_with_nothing_for_this_pc_is_a_different_kind_of_no()
    {
        // The model is listed for gpu PCs only; this one has no GPU. That is not
        // a memory shortage, so it must not be reported as one.
        var catalog = Catalog(Model("big", ["gpu"], ramGB: 2));

        var result = AiEligibility.Check(catalog, Profile(ram: 4 * Gigabyte, vram: 0));

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(StepDownReason.None);
    }

    [Theory]
    // Exactly the headroom boundary: available RAM = ramRequiredGB + 1 GB.
    [InlineData(3_000_000_000, true)]
    [InlineData(2_999_999_999, false)]
    public void The_memory_threshold_is_still_the_recommenders(ulong available, bool expected)
    {
        var catalog = Catalog(Model("tiny", ["cpu8"], ramGB: 2));

        AiEligibility.Check(catalog, Profile(available: available)).IsAvailable.Should().Be(expected);
    }

    [Theory]
    // Disk boundary: free space = size x 1.2.
    [InlineData(1_200_000_000, true)]
    [InlineData(1_199_999_999, false)]
    public void The_disk_threshold_is_still_the_recommenders(long disk, bool expected)
    {
        var catalog = Catalog(Model("tiny", ["cpu8"], ramGB: 2, sizeBytes: 1 * (long)Gigabyte));

        AiEligibility.Check(catalog, Profile(disk: disk)).IsAvailable.Should().Be(expected);
    }

    [Fact]
    public void A_small_model_listed_for_a_lower_tier_still_counts_on_a_bigger_pc()
    {
        // The Recommender steps down, so a minimal-tier model is offered to a
        // cpu16 machine when nothing larger fits. Availability has to agree.
        var catalog = Catalog(Model("tiny", ["minimal"], ramGB: 0.5, sizeBytes: 400_000_000));

        AiEligibility.Check(catalog, Profile(available: 2 * Gigabyte)).IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void An_empty_catalog_offers_nothing_without_blaming_memory()
    {
        var result = AiEligibility.Check(Catalog(), Profile());

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(StepDownReason.None);
    }

    // The four cases the 2026-10-02 source review asked to have pinned down.

    [Fact]
    public void A_larger_model_is_refused_even_though_a_smaller_one_fits()
    {
        // The review's example: 2.6 GB free admits the tiny entry, but the 1.7B
        // entry needs 2.5 GB plus the 1 GB headroom. Asking only "does anything
        // fit" would let the bigger model through.
        var tiny = Model("tiny", ["cpu8"], ramGB: 0.5, sizeBytes: 400_000_000);
        var bigger = Model("bigger", ["cpu8"], ramGB: 2.5, sizeBytes: 1_800_000_000);
        var catalog = Catalog(tiny, bigger);
        var profile = Profile(available: 2_600_000_000, ram: 8 * Gigabyte);

        AiEligibility.Check(catalog, profile).IsAvailable.Should().BeTrue("the tiny entry does fit");

        var result = AiEligibility.CheckFor(catalog, profile, bigger);

        result.IsAvailable.Should().BeFalse("the model actually being set up is judged as itself");
        result.Reason.Should().Be(StepDownReason.NotEnoughRam);
    }

    [Fact]
    public void A_known_model_that_fits_is_allowed()
    {
        var model = Model("tiny", ["cpu8"], ramGB: 0.5, sizeBytes: 400_000_000);

        var result = AiEligibility.CheckFor(
            Catalog(model), Profile(available: 2 * Gigabyte, ram: 8 * Gigabyte), model);

        result.IsAvailable.Should().BeTrue();
        result.Reason.Should().Be(StepDownReason.None);
    }

    [Fact]
    public void An_unknown_file_is_held_to_the_strictest_entry_for_this_pc()
    {
        // A hand-imported GGUF has no measured requirement, and its file size is
        // no substitute for one, so it is judged by the most demanding entry the
        // catalog has for a PC this size — never admitted merely because some
        // smaller entry happens to fit.
        var catalog = Catalog(
            Model("small", ["cpu8"], ramGB: 0.5, sizeBytes: 400_000_000),
            Model("large", ["cpu8"], ramGB: 3.0, sizeBytes: 2_000_000_000));

        // Room for the small entry, not for the large one.
        AiEligibility.CheckFor(catalog, Profile(available: 2_000_000_000, ram: 8 * Gigabyte), null)
            .IsAvailable.Should().BeFalse();

        // Room for both, so an unmeasured file is not refused outright.
        AiEligibility.CheckFor(catalog, Profile(available: 5 * Gigabyte, ram: 8 * Gigabyte), null)
            .IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void An_unknown_file_is_refused_when_nothing_is_listed_for_this_pc()
    {
        var catalog = Catalog(Model("gpu-only", ["gpu"], ramGB: 2));

        var result = AiEligibility.CheckFor(catalog, Profile(ram: 4 * Gigabyte), null);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(StepDownReason.None);
    }

    [Fact]
    public void An_unknown_file_is_refused_against_an_empty_catalog()
    {
        AiEligibility.CheckFor(Catalog(), Profile(), null).IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void The_same_model_can_pass_and_then_fail_when_the_machine_changes()
    {
        // Resource loss while a dialogue sits open: the same model, measured
        // again, must answer for the machine as it is now.
        var model = Model("tiny", ["cpu8"], ramGB: 0.5, sizeBytes: 400_000_000);
        var catalog = Catalog(model);

        AiEligibility.CheckFor(catalog, Profile(available: 2 * Gigabyte, ram: 8 * Gigabyte), model)
            .IsAvailable.Should().BeTrue();
        AiEligibility.CheckFor(catalog, Profile(available: 500_000_000, ram: 8 * Gigabyte), model)
            .IsAvailable.Should().BeFalse();
    }
}
