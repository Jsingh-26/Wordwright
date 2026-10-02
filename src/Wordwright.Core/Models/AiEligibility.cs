using Wordwright.Core.Hardware;

namespace Wordwright.Core.Models;

/// <summary>Whether offline AI can be offered on this PC at all.</summary>
public sealed record AiAvailability
{
    /// <summary>True when at least one catalog model fits this PC's memory and disk.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// What the PC is short of when <see cref="IsAvailable"/> is false.
    /// <see cref="StepDownReason.None"/> means the catalog lists nothing for this
    /// class of PC, which is a different kind of "no" from not enough memory.
    /// </summary>
    public required StepDownReason Reason { get; init; }
}

/// <summary>
/// Decides whether this PC has the memory and disk for anything the catalog knows
/// about, so the Offline AI option can be hidden and setup refused before a
/// download starts (docs/PLAN.md → "Maintainer requirement: startup resource
/// eligibility", 2026-10-02).
///
/// Two things this deliberately does <b>not</b> do:
/// <list type="bullet">
/// <item>It ignores approval. Availability is about what the PC can run, not
/// about whether a model has finished the evaluation in docs/EVAL.md, so a
/// capable PC still shows the "no model ready to offer yet" dialogue rather than
/// looking like a hardware failure.</item>
/// <item>It never asks the user to close anything. The memory and disk figures
/// are read as the machine is, under normal use, and the caller is expected to
/// call <see cref="Check"/> again before setup or use rather than trusting a
/// startup result.</item>
/// </list>
///
/// The thresholds are <see cref="Recommender"/>'s, unchanged and shared, so this
/// can never disagree with what is actually offered.
/// </summary>
public static class AiEligibility
{
    /// <summary>Checks <paramref name="profile"/> against the catalog.</summary>
    public static AiAvailability Check(ModelCatalog catalog, HardwareProfile profile)
    {
        var usable = Usable(catalog, profile);

        if (usable.Any(model => Recommender.HasRam(model, profile) && Recommender.HasDisk(model, profile)))
        {
            return new AiAvailability { IsAvailable = true, Reason = StepDownReason.None };
        }

        if (usable.Count == 0)
        {
            // Nothing is listed for a PC this size, so memory is not the problem.
            return new AiAvailability { IsAvailable = false, Reason = StepDownReason.None };
        }

        // Every model fits in memory but not on disk, or none fits in memory.
        return new AiAvailability
        {
            IsAvailable = false,
            Reason = usable.Any(model => Recommender.HasRam(model, profile))
                ? StepDownReason.NotEnoughDisk
                : StepDownReason.NotEnoughRam,
        };
    }

    /// <summary>
    /// Whether this PC can carry one <b>specific</b> model right now.
    ///
    /// A model the catalog knows is judged on its own numbers. A model it does
    /// not know — a hand-imported GGUF — has no measured requirement, and its
    /// file size is no substitute for one, so it is held to the strictest
    /// requirement the catalog defines for a PC of this size. That way an
    /// unmeasured file can never be admitted merely because some smaller catalog
    /// entry happens to fit.
    /// </summary>
    public static AiAvailability CheckFor(ModelCatalog catalog, HardwareProfile profile, CatalogEntry? model)
    {
        if (model is not null)
        {
            return Verdict(Recommender.HasRam(model, profile), Recommender.HasDisk(model, profile));
        }

        // Nothing to compare an unknown file against: there is no basis on which
        // to say it would run, so it is refused rather than guessed at.
        return UnknownRequirement(catalog, profile) is { } strictest
            ? Verdict(Recommender.HasRam(strictest, profile), Recommender.HasDisk(strictest, profile))
            : new AiAvailability { IsAvailable = false, Reason = StepDownReason.None };
    }

    private static AiAvailability Verdict(bool fitsMemory, bool fitsDisk) => new()
    {
        IsAvailable = fitsMemory && fitsDisk,
        Reason = fitsMemory && fitsDisk
            ? StepDownReason.None
            : fitsMemory ? StepDownReason.NotEnoughDisk : StepDownReason.NotEnoughRam,
    };

    /// <summary>The most demanding entry the catalog lists for a PC of this size.</summary>
    private static CatalogEntry? UnknownRequirement(ModelCatalog catalog, HardwareProfile profile)
    {
        var usable = Usable(catalog, profile);

        return usable.Count == 0
            ? null
            : usable.OrderByDescending(model => model.RamRequiredGB)
                .ThenByDescending(model => model.SizeBytes)
                .First();
    }

    /// <summary>The catalog entries listed for this PC's own tier or a smaller one.</summary>
    private static List<CatalogEntry> Usable(ModelCatalog catalog, HardwareProfile profile)
    {
        var tiers = Recommender.StepDownOrder(TierClassifier.Classify(profile))
            .Select(tier => tier.ToCatalogName())
            .ToHashSet(StringComparer.Ordinal);

        return [.. catalog.Models.Where(model => model.Tiers.Any(tiers.Contains))];
    }
}
