namespace Wordwright.Core.Actions;

/// <summary>
/// The six actions every install starts with (docs/UX_COPY.md → Built-in
/// actions). They are ordinary actions: the AI actions page edits, reorders and
/// disables them like any other, and "Reset to default" restores one from here.
/// </summary>
public static class ActionSeeds
{
    /// <summary>The built-ins, in the order the palette shows them.</summary>
    public static IReadOnlyList<AiAction> BuiltIns() =>
    [
        new()
        {
            Id = "fix",
            Name = "Fix grammar and spelling",
            ShortcutKey = "G",
            Instruction = "Correct grammar, spelling and punctuation. "
                + "Keep the meaning, tone and language. Change as little as possible.",
            Hotkey = "Ctrl+Alt+G",
            BuiltIn = true,
        },
        new()
        {
            Id = "clear",
            Name = "Make it clearer",
            ShortcutKey = "C",
            Instruction = "Rewrite so it is clear and easy to read. "
                + "Keep the meaning and roughly the same length.",
            BuiltIn = true,
        },
        new()
        {
            Id = "formal",
            Name = "More formal",
            ShortcutKey = "F",
            Instruction = "Rewrite in a polite, professional tone suitable for work email. "
                + "Keep the meaning.",
            BuiltIn = true,
        },
        new()
        {
            Id = "friendly",
            Name = "More friendly",
            ShortcutKey = "R",
            Instruction = "Rewrite in a warm, friendly, natural tone. Keep the meaning.",
            BuiltIn = true,
        },
        new()
        {
            Id = "shorten",
            Name = "Shorten",
            ShortcutKey = "S",
            Instruction = "Make it shorter and more direct. Keep every important point.",
            BuiltIn = true,
        },
        new()
        {
            Id = "custom",
            Name = "Custom instruction…",
            ShortcutKey = "I",
            Instruction = "",
            BuiltIn = true,
        },
    ];

    /// <summary>The document every install starts with.</summary>
    public static ActionDocument Default() => new() { Actions = BuiltIns() };

    /// <summary>The default form of a built-in action, for "Reset to default",
    /// or null when <paramref name="id"/> is not a built-in.</summary>
    public static AiAction? BuiltIn(string id) =>
        BuiltIns().FirstOrDefault(action => string.Equals(action.Id, id, StringComparison.Ordinal));
}
