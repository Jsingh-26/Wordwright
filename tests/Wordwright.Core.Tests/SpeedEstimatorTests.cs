using FluentAssertions;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

public class SpeedEstimatorTests
{
    /// <summary>The 2B model on a cpu8 machine, from docs/MODELS.md.</summary>
    private static CatalogSpeed TwoBOnCpu8 => new()
    {
        PromptTps = [60, 150],
        GenTps = [15, 30],
        Provisional = true,
    };

    [Fact]
    public void TheWorkedExample_promisesTwoToFourSecondsForOneLine()
    {
        var range = SpeedEstimator.EstimateRange(TwoBOnCpu8, EstimateJob.OneLine);

        range.FastestSeconds.Should().Be(2);
        range.SlowestSeconds.Should().Be(4);
    }

    [Fact]
    public void TheWorkedExample_promisesFourToNineSecondsForAShortParagraph()
    {
        var range = SpeedEstimator.EstimateRange(TwoBOnCpu8, EstimateJob.ShortParagraph);

        range.FastestSeconds.Should().Be(4);
        range.SlowestSeconds.Should().Be(9);
    }

    [Fact]
    public void AFasterMachine_promisesLess()
    {
        var gpu = new CatalogSpeed { PromptTps = [800, 2500], GenTps = [70, 150] };

        // 140/2500 + 30/150 = 0.26 s; 140/800 + 30/70 = 0.6 s.
        var range = SpeedEstimator.EstimateRange(gpu, EstimateJob.OneLine);

        range.FastestSeconds.Should().Be(0);
        range.SlowestSeconds.Should().Be(1);
    }

    [Fact]
    public void MeasuredSpeeds_roundToTheNearestHalfSecond()
    {
        // 140/60 + 30/15 = 4.33 s.
        var duration = SpeedEstimator.EstimateDuration(60, 15, EstimateJob.OneLine);

        duration.Should().Be(TimeSpan.FromSeconds(4.5));
    }

    [Fact]
    public void MeasuredSpeeds_roundDownWhenNearer()
    {
        // 190/200 + 90/40 = 3.2 s, nearer three than three and a half.
        var duration = SpeedEstimator.EstimateDuration(200, 40, EstimateJob.ShortParagraph);

        duration.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void ACatalogWithoutUsableSpeeds_promisesNothing()
    {
        var range = SpeedEstimator.EstimateRange(new CatalogSpeed(), EstimateJob.OneLine);

        range.FastestSeconds.Should().Be(0);
        range.SlowestSeconds.Should().Be(0);
    }

    [Fact]
    public void MeasuredSpeedsWithoutAReading_promiseNothing()
    {
        SpeedEstimator.EstimateDuration(0, 0, EstimateJob.OneLine).Should().Be(TimeSpan.Zero);
    }
}
