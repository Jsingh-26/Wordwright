using Wordwright.Core.Hardware;

namespace Wordwright.Core.Models;

/// <summary>Why the model offered is smaller than the PC's own tier could carry.</summary>
public enum StepDownReason
{
    /// <summary>Nothing smaller to step down to, or nothing was offered for the
    /// tier the PC has.</summary>
    None,

    /// <summary>The bigger models want more memory than is free right now.</summary>
    NotEnoughRam,

    /// <summary>The bigger models do not fit on the disk.</summary>
    NotEnoughDisk,
}

/// <summary>What to offer this PC (docs/MODELS.md → Recommendation).</summary>
public sealed record ModelRecommendation
{
    public required CatalogEntry Model { get; init; }

    /// <summary>The tier the offer is made for; below the PC's own when the
    /// bigger ones did not fit.</summary>
    public required HardwareTier Tier { get; init; }

    public required StepDownReason Reason { get; init; }

    public required SpeedRange OneLine { get; init; }

    public required SpeedRange ShortParagraph { get; init; }

    /// <summary>The rest of the approved models that fit this tier, best first.</summary>
    public required IReadOnlyList<CatalogEntry> OtherOptions { get; init; }

    public bool SteppedDown => Reason != StepDownReason.None;

    /// <summary>The catalog's speed range for a model at the tier this
    /// recommendation is for, for showing alongside the other options.</summary>
    public CatalogSpeed? SpeedFor(CatalogEntry entry) => entry.SpeedFor(Tier.ToCatalogName());
}

/// <summary>
/// Chooses the model to offer (docs/MODELS.md → Recommendation): the approved
/// models that fit the PC, best score first, stepping down a tier if the bigger
/// ones cannot be run right now.
/// </summary>
public static class Recommender
{
    /// <summary>Memory the PC needs beyond the model's own requirement.
    /// Shared with <see cref="AiEligibility"/>, so the two cannot disagree.</summary>
    internal const double RamHeadroomGB = 1;

    /// <summary>Disk the download needs, as a multiple of the file size.
    /// Shared with <see cref="AiEligibility"/>, so the two cannot disagree.</summary>
    internal const double DiskHeadroomFactor = 1.2;

    /// <summary>
    /// The best model for this PC, or null when no approved model fits anywhere —
    /// which the caller shows as "free up space" or "this PC can only run a very
    /// small model".
    /// </summary>
    public static ModelRecommendation? Recommend(ModelCatalog catalog, HardwareProfile profile)
    {
        var tier = TierClassifier.Classify(profile);
        var reason = StepDownReason.None;

        foreach (var candidate in StepDownOrder(tier))
        {
            var offered = catalog.Models
                .Where(model => model.IsApproved && model.Tiers.Contains(candidate.ToCatalogName()))
                .OrderByDescending(model => model.EvalScores?.Overall ?? 0)
                .ThenBy(model => model.SizeBytes)
                .ToList();

            var runnable = offered.Where(model => HasRam(model, profile)).ToList();
            var room = runnable.Where(model => HasDisk(model, profile)).ToList();

            if (room.Count > 0)
            {
                var best = room[0];
                var speed = best.SpeedFor(candidate.ToCatalogName());

                return new ModelRecommendation
                {
                    Model = best,
                    Tier = candidate,
                    // Only the PC's own tier needs no explanation.
                    Reason = candidate == tier ? StepDownReason.None : reason,
                    OneLine = SpeedEstimator.EstimateRange(speed ?? new CatalogSpeed(), EstimateJob.OneLine),
                    ShortParagraph = SpeedEstimator.EstimateRange(speed ?? new CatalogSpeed(), EstimateJob.ShortParagraph),
                    OtherOptions = [.. room.Skip(1)],
                };
            }

            // This tier has nothing to offer. Remember what was missing first,
            // because that is what the offered tier has to explain.
            if (reason == StepDownReason.None)
            {
                reason = offered.Count > runnable.Count
                    ? StepDownReason.NotEnoughRam
                    : offered.Count > room.Count ? StepDownReason.NotEnoughDisk : StepDownReason.None;
            }
        }

        return null;
    }

    /// <summary>The PC's tier first, then the smaller ones.</summary>
    internal static IEnumerable<HardwareTier> StepDownOrder(HardwareTier tier) => tier switch
    {
        HardwareTier.Gpu => [HardwareTier.Gpu, HardwareTier.Cpu16, HardwareTier.Cpu8, HardwareTier.Minimal],
        HardwareTier.Cpu16 => [HardwareTier.Cpu16, HardwareTier.Cpu8, HardwareTier.Minimal],
        HardwareTier.Cpu8 => [HardwareTier.Cpu8, HardwareTier.Minimal],
        _ => [HardwareTier.Minimal],
    };

    internal static bool HasRam(CatalogEntry model, HardwareProfile profile) =>
        profile.AvailableRamBytes / 1e9 >= model.RamRequiredGB + RamHeadroomGB;

    internal static bool HasDisk(CatalogEntry model, HardwareProfile profile) =>
        profile.FreeDiskBytes >= model.SizeBytes * DiskHeadroomFactor;
}
