namespace Wordwright.Core.Actions;

/// <summary>The two messages handed to the model for one rewrite.</summary>
public sealed record PromptMessages(string System, string User);

/// <summary>
/// Builds the prompt for a rewrite (docs/ARCHITECTURE.md → Prompt) and holds the
/// generation limits from the same section. The system message is fixed so every
/// action produces a bare rewrite, and the user message carries the action's
/// instruction followed by the selected text.
/// </summary>
public static class PromptBuilder
{
    /// <summary>Context 4,096 tokens in v1.</summary>
    public const int ContextTokens = 4096;

    /// <summary>Input beyond this many tokens shows the "too long" copy instead
    /// of rewriting (docs/UX_COPY.md → Pill.TooLong).</summary>
    public const int MaxInputTokens = 1500;

    /// <summary>No rewrite may generate more than this many new tokens.</summary>
    public const int MaxNewTokensCap = 1536;

    public const float Temperature = 0.3f;

    public const float TopP = 0.9f;

    public const float RepeatPenalty = 1.05f;

    public const string SystemMessage =
        "You rewrite text. Follow the instruction exactly.\n"
        + "Reply with only the rewritten text: no introduction, no quotes, no notes, no explanation.\n"
        + "Keep the original language unless the instruction says otherwise.\n"
        + "Keep names, numbers, links and formatting such as line breaks and bullet points.";

    public static PromptMessages Build(string instruction, string text) => new(
        SystemMessage,
        $"Instruction: {instruction}\n\nText:\n{text}");

    /// <summary><c>min(2 × input tokens + 64, 1,536)</c> (docs/ARCHITECTURE.md →
    /// Generation parameters).</summary>
    public static int MaxNewTokens(int inputTokens) => Math.Min((2 * inputTokens) + 64, MaxNewTokensCap);
}
