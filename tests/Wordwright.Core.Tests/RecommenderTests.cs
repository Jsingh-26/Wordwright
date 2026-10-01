using FluentAssertions;
using Wordwright.Core.Hardware;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

public class RecommenderTests
{
    private const ulong Gigabyte = 1_000_000_000;

    private static HardwareProfile Profile(
        ulong ram = 16 * Gigabyte,
        ulong available = 15 * Gigabyte,
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
        double overall,
        double ramGB = 2,
        long sizeBytes = 1 * (long)Gigabyte,
        string status = "approved",
        double[]? speed = null) => new()
        {
            Id = id,
            DisplayName = id,
            Tiers = tiers,
            RamRequiredGB = ramGB,
            SizeBytes = sizeBytes,
            Sha256 = "aa",
            Status = status,
            EvalScores = overall > 0 ? new EvalScores { Overall = overall } : null,
            Speed = new Dictionary<string, CatalogSpeed>
            {
                [tiers[0]] = new CatalogSpeed
                {
                    PromptTps = speed ?? [60, 150],
                    GenTps = [15, 30],
                },
            },
        };

    private static ModelCatalog Catalog(params CatalogEntry[] models) => new()
    {
        SchemaVersion = 1,
        CatalogVersion = "2026-10-01",
        Models = models,
    };

    [Fact]
    public void TheBestScoringModelForTheTier_isChosen()
    {
        var catalog = Catalog(
            Model("good", ["cpu16"], overall: 3.2),
            Model("better", ["cpu16"], overall: 4.1),
            Model("worse", ["cpu16"], overall: 2.4));

        var recommendation = Recommender.Recommend(catalog, Profile())!;

        recommendation.Model.Id.Should().Be("better");
        recommendation.Tier.Should().Be(HardwareTier.Cpu16);
        recommendation.SteppedDown.Should().BeFalse();
        recommendation.OtherOptions.Select(model => model.Id).Should().Equal("good", "worse");
    }

    [Fact]
    public void OnATie_theSmallerFileWins()
    {
        var catalog = Catalog(
            Model("big", ["cpu16"], overall: 3.5, sizeBytes: 3 * (long)Gigabyte),
            Model("small", ["cpu16"], overall: 3.5, sizeBytes: 1 * (long)Gigabyte));

        Recommender.Recommend(catalog, Profile())!.Model.Id.Should().Be("small");
    }

    [Fact]
    public void Candidates_areNotOffered()
    {
        var catalog = Catalog(
            Model("candidate", ["cpu16"], overall: 4.9, status: "candidate"),
            Model("approved", ["cpu16"], overall: 3.0));

        Recommender.Recommend(catalog, Profile())!.Model.Id.Should().Be("approved");
    }

    [Fact]
    public void WithNothingForThePcsOwnTier_theNextOneDownIsUsed()
    {
        var catalog = Catalog(Model("cpu8-only", ["cpu8"], overall: 4.5));

        var recommendation = Recommender.Recommend(catalog, Profile())!;

        recommendation.Tier.Should().Be(HardwareTier.Cpu8);
        recommendation.Model.Id.Should().Be("cpu8-only");
        recommendation.Reason.Should().Be(
            StepDownReason.None, "nothing was missing — the catalog simply has nothing for cpu16");
    }

    [Fact]
    public void AModelThatCannotHaveEnoughMemoryOfItsOwn_losesToASmallerOne()
    {
        // 15 GB free: the big model wants 15 + 1 GB, the small one 2 + 1.
        var catalog = Catalog(
            Model("fourB", ["cpu16"], overall: 4.5, ramGB: 15),
            Model("twoB", ["cpu16"], overall: 3.0, ramGB: 2));

        Recommender.Recommend(catalog, Profile())!.Model.Id.Should().Be("twoB");
    }

    [Fact]
    public void WhenNothingOnThePcsTierRuns_weStepDownAndSayWhy()
    {
        // The smaller model is only offered for cpu8, so using it means moving
        // down a tier — and the memory shortage is why.
        var catalog = Catalog(
            Model("fourB", ["cpu16"], overall: 4.5, ramGB: 15),
            Model("twoB", ["cpu8"], overall: 3.0, ramGB: 2));

        var recommendation = Recommender.Recommend(catalog, Profile())!;

        recommendation.Model.Id.Should().Be("twoB");
        recommendation.Tier.Should().Be(HardwareTier.Cpu8);
        recommendation.SteppedDown.Should().BeTrue();
        recommendation.Reason.Should().Be(StepDownReason.NotEnoughRam);
    }

    [Fact]
    public void AModelTooBigForTheDisk_losesToOneThatFits()
    {
        // 10 GB free: 8 GB × 1.2 does not fit, 1 GB does.
        var catalog = Catalog(
            Model("huge", ["cpu16"], overall: 4.5, sizeBytes: 10 * (long)Gigabyte),
            Model("small", ["cpu16"], overall: 3.0));

        Recommender.Recommend(catalog, Profile(disk: 10 * (long)Gigabyte))!.Model.Id.Should().Be("small");
    }

    [Fact]
    public void WhenNothingFitsTheDiskAtAll_thereIsNoRecommendation()
    {
        var catalog = Catalog(Model("huge", ["cpu16"], overall: 4.5, sizeBytes: 50 * (long)Gigabyte));

        Recommender.Recommend(catalog, Profile(disk: 10 * (long)Gigabyte)).Should().BeNull();
    }

    [Fact]
    public void AGpuMachine_getsTheGpuModel()
    {
        var catalog = Catalog(
            Model("gpu", ["gpu"], overall: 3.0, speed: [800, 2500]),
            Model("cpu", ["cpu16"], overall: 4.5));

        var recommendation = Recommender.Recommend(catalog, Profile(vram: 8 * Gigabyte))!;

        recommendation.Model.Id.Should().Be("gpu");
        recommendation.Tier.Should().Be(HardwareTier.Gpu);
    }

    [Fact]
    public void TheRecommendation_carriesBothEstimates()
    {
        var catalog = Catalog(Model("twoB", ["cpu16"], overall: 3.0));

        var recommendation = Recommender.Recommend(catalog, Profile())!;

        // The docs/MODELS.md worked example, for the tier's own speed range.
        recommendation.OneLine.FastestSeconds.Should().Be(2);
        recommendation.OneLine.SlowestSeconds.Should().Be(4);
        recommendation.ShortParagraph.FastestSeconds.Should().Be(4);
        recommendation.ShortParagraph.SlowestSeconds.Should().Be(9);
        recommendation.SpeedFor(recommendation.Model).Should().NotBeNull();
    }

    [Fact]
    public void AMinimalPc_onlyEverGetsAMinimalModel()
    {
        var catalog = Catalog(
            Model("tiny", ["minimal"], overall: 2.0),
            Model("twoB", ["cpu8"], overall: 4.5));

        var recommendation = Recommender.Recommend(catalog, Profile(ram: 4 * Gigabyte, available: 3 * Gigabyte))!;

        recommendation.Model.Id.Should().Be("tiny");
        recommendation.Tier.Should().Be(HardwareTier.Minimal);
    }
}
