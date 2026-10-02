using Wordwright.App.Resources;
using Wordwright.Core.Hardware;
using Wordwright.Core.Models;
using Wordwright.Platform.Hardware;

namespace Wordwright.App.Ai;

/// <summary>
/// The live answer to "can this PC run offline AI right now?" (docs/PLAN.md →
/// "Maintainer requirement: startup resource eligibility", 2026-10-02).
///
/// Every call measures the machine again. Reusing the startup result is exactly
/// what the requirement forbids: a PC that lost memory since then would
/// otherwise be offered, given or asked to load a model it cannot carry.
/// </summary>
internal static class AiGate
{
    /// <summary>Measures the machine off the UI thread — WMI and DXGI block.</summary>
    public static Task<AiAvailability> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Check(HardwareProbe.Read()), cancellationToken);

    /// <summary>The same check for a profile the caller has just read.</summary>
    public static AiAvailability Check(HardwareProfile profile) =>
        AiEligibility.Check(CatalogParser.Embedded(), profile);

    /// <summary>The refusal in the user's words (docs/UX_COPY.md → Ai.Unavailable.*).</summary>
    public static string Refusal(AiAvailability availability) => availability.Reason switch
    {
        StepDownReason.NotEnoughRam => Strings.Get("Ai.Unavailable.Ram"),
        StepDownReason.NotEnoughDisk => Strings.Get("Ai.Unavailable.Disk"),
        _ => Strings.Get("Ai.Unavailable.None"),
    };
}
