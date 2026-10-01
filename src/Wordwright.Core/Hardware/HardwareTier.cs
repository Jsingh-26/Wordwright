namespace Wordwright.Core.Hardware;

/// <summary>
/// How much AI a PC can carry (docs/MODELS.md → Hardware tiers). The names match
/// the catalog's <c>tiers</c> list, which is why they are spelled out below.
/// </summary>
public enum HardwareTier
{
    /// <summary>A GPU with at least 6 GB of its own memory.</summary>
    Gpu,

    /// <summary>16 GB of memory or more, with AVX2.</summary>
    Cpu16,

    /// <summary>8 GB of memory or more, with AVX2.</summary>
    Cpu8,

    /// <summary>Anything less: a tiny model, or no AI at all.</summary>
    Minimal,
}

public static class HardwareTierNames
{
    public const string Gpu = "gpu";

    public const string Cpu16 = "cpu16";

    public const string Cpu8 = "cpu8";

    public const string Minimal = "minimal";

    /// <summary>The tier's name as the catalog and settings files spell it.</summary>
    public static string ToCatalogName(this HardwareTier tier) => tier switch
    {
        HardwareTier.Gpu => Gpu,
        HardwareTier.Cpu16 => Cpu16,
        HardwareTier.Cpu8 => Cpu8,
        _ => Minimal,
    };
}
