namespace Wordwright.Core.Models;

/// <summary>Which of the two reference jobs an estimate is for.</summary>
public enum EstimateJob
{
    /// <summary>About 20 words: 140 prompt tokens, 30 generated.</summary>
    OneLine,

    /// <summary>About 60 words: 190 prompt tokens, 90 generated.</summary>
    ShortParagraph,
}

/// <summary>A promise of how long something takes, in whole seconds.</summary>
public sealed record SpeedRange(int FastestSeconds, int SlowestSeconds);

/// <summary>
/// How long a rewrite should take (docs/MODELS.md → Speed estimate):
/// <c>seconds = promptTokens / promptTokensPerSec + outputTokens / genTokensPerSec</c>.
/// Before a model is downloaded the answer is a range from the catalog's speeds;
/// after calibration it is a single number.
/// </summary>
public static class SpeedEstimator
{
    private const int OneLinePromptTokens = 140;
    private const int OneLineOutputTokens = 30;
    private const int ParagraphPromptTokens = 190;
    private const int ParagraphOutputTokens = 90;

    /// <summary>The slowest and fastest this job should take, from a catalog
    /// range. The ends come from the two ends of both rates, so the promise
    /// covers everything the machine might do.</summary>
    public static SpeedRange EstimateRange(CatalogSpeed speed, EstimateJob job) => new(
        FastestSeconds: Round(Seconds(speed.FastestPrompt, speed.FastestGen, job)),
        SlowestSeconds: Round(Seconds(speed.SlowestPrompt, speed.SlowestGen, job)));

    /// <summary>The same job from measured rates, rounded to the nearest half
    /// second, as calibrated models are shown.</summary>
    public static TimeSpan EstimateDuration(double promptTokensPerSecond, double genTokensPerSecond, EstimateJob job)
    {
        var seconds = Seconds(promptTokensPerSecond, genTokensPerSecond, job);

        return TimeSpan.FromSeconds(Math.Round(seconds * 2, MidpointRounding.AwayFromZero) / 2);
    }

    private static double Seconds(double promptTokensPerSecond, double genTokensPerSecond, EstimateJob job)
    {
        var (promptTokens, outputTokens) = Tokens(job);

        if (promptTokensPerSecond <= 0 || genTokensPerSecond <= 0)
        {
            // A catalog with no usable speed cannot promise anything.
            return 0;
        }

        return promptTokens / promptTokensPerSecond + outputTokens / genTokensPerSecond;
    }

    private static (int Prompt, int Output) Tokens(EstimateJob job) => job switch
    {
        EstimateJob.OneLine => (OneLinePromptTokens, OneLineOutputTokens),
        _ => (ParagraphPromptTokens, ParagraphOutputTokens),
    };

    private static int Round(double seconds) => (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
}
