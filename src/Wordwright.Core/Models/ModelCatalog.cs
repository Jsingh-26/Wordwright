namespace Wordwright.Core.Models;

/// <summary>
/// The curated model catalog, <c>models/models.json</c>
/// (docs/MODELS.md → Catalog file). Fields we do not know are ignored, so a
/// catalog written by a newer minor version still loads.
/// </summary>
public sealed record ModelCatalog
{
    public int SchemaVersion { get; init; }

    /// <summary>A date string, bumped on every edit of the catalog.</summary>
    public string CatalogVersion { get; init; } = "";

    public IReadOnlyList<CatalogEntry> Models { get; init; } = [];
}

/// <summary>One model in the catalog.</summary>
public sealed record CatalogEntry
{
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Publisher { get; init; } = "";

    /// <summary>"2B", "4B" and so on, as the publisher writes it.</summary>
    public string Parameters { get; init; } = "";

    public string Quantization { get; init; } = "";

    public string License { get; init; } = "";

    public string LicenseUrl { get; init; } = "";

    public CatalogSource Source { get; init; } = new();

    public long SizeBytes { get; init; }

    public string Sha256 { get; init; } = "";

    public double RamRequiredGB { get; init; }

    /// <summary>The tiers this model is offered on, by catalog name.</summary>
    public IReadOnlyList<string> Tiers { get; init; } = [];

    public IReadOnlyList<string> Strengths { get; init; } = [];

    public IReadOnlyList<string> Weaknesses { get; init; } = [];

    public PromptHints PromptHints { get; init; } = new();

    /// <summary>Speed ranges per tier, keyed by catalog tier name.</summary>
    public IReadOnlyDictionary<string, CatalogSpeed> Speed { get; init; } =
        new Dictionary<string, CatalogSpeed>();

    public EvalScores? EvalScores { get; init; }

    /// <summary>"candidate" until the evaluation in docs/EVAL.md approves it.</summary>
    public string Status { get; init; } = "candidate";

    public bool IsApproved => string.Equals(Status, "approved", StringComparison.OrdinalIgnoreCase);

    /// <summary>The speed range for a tier, or null when the catalog has none.</summary>
    public CatalogSpeed? SpeedFor(string tierName) =>
        Speed.TryGetValue(tierName, out var speed) ? speed : null;
}

/// <summary>Where the weights come from: the publisher's own repository.</summary>
public sealed record CatalogSource
{
    public string Repo { get; init; } = "";

    public string File { get; init; } = "";

    public string Url { get; init; } = "";
}

/// <summary>What the model needs to be told about itself.</summary>
public sealed record PromptHints
{
    /// <summary>How to keep a model that "thinks" out loud from doing it.</summary>
    public string DisableThinking { get; init; } = "";
}

/// <summary>A token-rate range for one tier, provisional until measured.</summary>
public sealed record CatalogSpeed
{
    /// <summary>Prompt tokens per second, slowest and fastest.</summary>
    public IReadOnlyList<double> PromptTps { get; init; } = [];

    /// <summary>Generated tokens per second, slowest and fastest.</summary>
    public IReadOnlyList<double> GenTps { get; init; } = [];

    /// <summary>How much longer the first rewrite after a cold start takes while
    /// the weights are read in (docs/MODELS.md → Speed estimate).</summary>
    public int LoadSeconds { get; init; }

    /// <summary>True while these are estimates rather than measurements.</summary>
    public bool Provisional { get; init; }

    public double SlowestPrompt => PromptTps.Count > 0 ? PromptTps.Min() : 0;

    public double FastestPrompt => PromptTps.Count > 0 ? PromptTps.Max() : 0;

    public double SlowestGen => GenTps.Count > 0 ? GenTps.Min() : 0;

    public double FastestGen => GenTps.Count > 0 ? GenTps.Max() : 0;
}

/// <summary>
/// The evaluation scores (docs/EVAL.md). Only <c>overall</c> matters to the
/// recommendation; the per-action scores sit beside it in the file and are
/// ignored here.
/// </summary>
public sealed record EvalScores
{
    public double Overall { get; init; }
}
