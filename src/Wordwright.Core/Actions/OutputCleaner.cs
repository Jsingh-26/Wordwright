using System.Text;
using System.Text.RegularExpressions;

namespace Wordwright.Core.Actions;

/// <summary>Why the model's answer was not usable as a rewrite.</summary>
public enum OutputRejection
{
    /// <summary>It cleaned up to something worth pasting.</summary>
    None,

    /// <summary>Nothing was left once the scaffolding was removed.</summary>
    Empty,

    /// <summary>Far longer than the input, so the model ran away
    /// (docs/ARCHITECTURE.md → OutputCleaner rules, rule 6).</summary>
    Runaway,
}

/// <summary>The clean text, or the reason it was rejected.</summary>
public sealed record CleanedOutput(string Text, OutputRejection Rejection)
{
    public bool Accepted => Rejection == OutputRejection.None;
}

/// <summary>
/// Turns what a model wrote into the text the user actually wants
/// (docs/ARCHITECTURE.md → OutputCleaner rules). Every rule has a test, because
/// a small local model gets several of them wrong often.
/// </summary>
public static class OutputCleaner
{
    /// <summary>A <c>&lt;think&gt;…&lt;/think&gt;</c> block.</summary>
    private static readonly Regex ThinkingBlock = new(
        @"<think\b[^>]*>.*?</think>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>A thinking block the model opened and never closed: drop the rest.</summary>
    private static readonly Regex UnclosedThinking = new(
        @"<think\b[^>]*>.*$",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>Rule 2: a line that only announces the rewrite.</summary>
    private static readonly Regex Preamble = new(
        @"^(sure|okay|of course|certainly|here('s| is| are)|rewritten|corrected|revised)\b.*:?\s*$",
        RegexOptions.IgnoreCase);

    /// <summary>Rule 4: lines a model adds to explain itself.</summary>
    private static readonly string[] NotePrefixes =
        ["Note:", "Explanation:", "(Note", "I changed", "I made", "I corrected", "Changes:"];

    /// <summary>Rule 6: a rewrite may not be longer than this, or it ran away.</summary>
    private const int RunawayFactor = 4;

    private const int RunawaySlack = 200;

    /// <summary>Cleans <paramref name="output"/>, written in reply to
    /// <paramref name="input"/>.</summary>
    public static CleanedOutput Clean(string input, string output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var text = ThinkingBlock.Replace(output, "");
        text = UnclosedThinking.Replace(text, "");

        text = RemovePreamble(text);
        text = RemoveWrapping(text, InputIsWrapped(input));
        text = RemoveTrailingNotes(text);

        text = text.Trim();

        if (text.Length == 0)
        {
            return new CleanedOutput("", OutputRejection.Empty);
        }

        if (text.Length > ((long)RunawayFactor * input.Length) + RunawaySlack)
        {
            return new CleanedOutput("", OutputRejection.Runaway);
        }

        // Rule 5: a body that ended in a newline keeps one, so a snippet the user
        // was editing does not lose its last line break.
        if (input.EndsWith('\n'))
        {
            text += "\n";
        }

        return new CleanedOutput(text, OutputRejection.None);
    }

    /// <summary>Rule 2: drop the lines at the top that only say "here you go".</summary>
    private static string RemovePreamble(string text)
    {
        var lines = Split(text);
        var start = 0;

        while (start < lines.Count && Preamble.IsMatch(lines[start]))
        {
            start++;
        }

        return start == 0 ? text : Join(lines, start, lines.Count);
    }

    /// <summary>
    /// Rule 3: strip one pair of wrapping quotes or one wrapping code fence, but
    /// only when the input was not wrapped that way itself — a rewrite of already
    /// quoted text should keep its quotes.
    /// </summary>
    private static string RemoveWrapping(string text, bool inputIsWrapped)
    {
        if (inputIsWrapped)
        {
            return text;
        }

        var trimmed = text.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var openEnd = trimmed.IndexOf('\n');
            var closeStart = trimmed.LastIndexOf("```", StringComparison.Ordinal);

            if (openEnd >= 0 && closeStart > openEnd)
            {
                return trimmed[(openEnd + 1)..closeStart];
            }
        }

        if (trimmed.Length >= 2 && IsClosingQuote(trimmed[0], trimmed[^1]))
        {
            return trimmed[1..^1];
        }

        return text;
    }

    /// <summary>Rule 4: cut a note the model tacked on at the end.</summary>
    private static string RemoveTrailingNotes(string text)
    {
        var lines = Split(text);

        for (var index = 0; index < lines.Count; index++)
        {
            if (!StartsNote(lines[index]))
            {
                continue;
            }

            // Only a note that runs to the end counts: a "Note:" followed by more
            // rewritten text is part of the rewrite.
            if (HasParagraphAfter(lines, index))
            {
                continue;
            }

            return Join(lines, 0, index);
        }

        return text;
    }

    private static bool HasParagraphAfter(List<string> lines, int index)
    {
        for (var next = index + 1; next < lines.Count; next++)
        {
            if (lines[next].Trim().Length == 0
                && next + 1 < lines.Count
                && lines[next + 1].Trim().Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Join(List<string> lines, int start, int end)
    {
        var builder = new StringBuilder();

        for (var index = start; index < end; index++)
        {
            if (index > start)
            {
                builder.Append('\n');
            }

            builder.Append(lines[index]);
        }

        // Trailing blank lines left behind by a removed note go with it.
        return builder.ToString().TrimEnd('\r', '\n', ' ', '\t');
    }

    private static List<string> Split(string text) =>
        [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

    private static bool StartsNote(string line)
    {
        var trimmed = line.TrimStart();

        return NotePrefixes.Any(prefix =>
            trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool InputIsWrapped(string input)
    {
        var trimmed = input.Trim();

        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || (trimmed.Length > 0 && IsOpenQuote(trimmed[0]));
    }

    private static bool IsOpenQuote(char character) => character is '"' or '\'' or '“' or '‘';

    private static bool IsClosingQuote(char open, char close) => open switch
    {
        '"' or '“' => close is '"' or '”',
        '\'' or '‘' => close is '\'' or '’',
        _ => false,
    };
}
