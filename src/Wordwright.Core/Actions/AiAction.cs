namespace Wordwright.Core.Actions;

/// <summary>
/// One AI action, as stored in <c>actions.json</c> (docs/ARCHITECTURE.md). The
/// six built-in actions come from docs/UX_COPY.md; users may add, edit, reorder
/// and disable actions, and reset a built-in to its default.
/// </summary>
public sealed record AiAction
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>The single letter that picks this action in the palette.</summary>
    public string ShortcutKey { get; init; } = "";

    /// <summary>What to tell the model. Empty for <c>custom</c>, whose
    /// instruction the user types in the palette.</summary>
    public string Instruction { get; init; } = "";

    /// <summary>The direct hotkey (e.g. <c>Ctrl+Alt+G</c>), or null when the
    /// action is palette-only (docs/ARCHITECTURE.md → actions.json).</summary>
    public string? Hotkey { get; init; }

    /// <summary>True for one of the actions from docs/UX_COPY.md, which can be
    /// reset to its default.</summary>
    public bool BuiltIn { get; init; }

    public bool Enabled { get; init; } = true;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

/// <summary>The whole <c>actions.json</c> document.</summary>
public sealed record ActionDocument
{
    public int SchemaVersion { get; init; } = ActionStore.SchemaVersion;

    public IReadOnlyList<AiAction> Actions { get; init; } = [];
}
