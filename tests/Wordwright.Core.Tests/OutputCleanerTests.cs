using FluentAssertions;
using Wordwright.Core.Actions;

namespace Wordwright.Core.Tests;

public class OutputCleanerTests
{
    private const string Input = "this is teh text";

    /// <summary>A thinking tag, built rather than typed so no editor can eat it.</summary>
    private static string Think(string content, bool close) =>
        "<think>" + content + (close ? "</think>" : "");

    [Fact]
    public void Clean_keepsAPlainRewriteExactly()
    {
        var result = OutputCleaner.Clean(Input, "This is the text.");

        result.Accepted.Should().BeTrue();
        result.Text.Should().Be("This is the text.");
    }

    // Rule 1
    [Fact]
    public void Clean_removesAThinkingBlock()
    {
        var result = OutputCleaner.Clean(
            Input, Think("The user wants grammar fixed.", close: true) + "This is the text.");

        result.Text.Should().Be("This is the text.");
    }

    [Fact]
    public void Clean_removesAnUnclosedThinkingBlockAndEverythingAfterIt()
    {
        var output = "This is the text.\n" + Think("and now I keep talking", close: false);

        var result = OutputCleaner.Clean(Input, output);

        result.Text.Should().Be("This is the text.");
    }

    // Rule 2
    [Theory]
    [InlineData("Sure, here you go:\n\nThis is the text.")]
    [InlineData("Of course:\nThis is the text.")]
    [InlineData("Certainly! Here's the rewritten text:\nThis is the text.")]
    [InlineData("Rewritten:\nThis is the text.")]
    public void Clean_removesLeadingPreambles(string output)
    {
        var result = OutputCleaner.Clean(Input, output);

        result.Text.Should().Be("This is the text.");
    }

    // Rule 3
    [Fact]
    public void Clean_removesOnePairOfWrappingQuotes()
    {
        var result = OutputCleaner.Clean(Input, "\"This is the text.\"");

        result.Text.Should().Be("This is the text.");
    }

    [Fact]
    public void Clean_removesSmartQuotesToo()
    {
        var result = OutputCleaner.Clean(Input, "“This is the text.”");

        result.Text.Should().Be("This is the text.");
    }

    [Fact]
    public void Clean_removesAWrappingCodeFence()
    {
        var result = OutputCleaner.Clean(Input, "```\nThis is the text.\n```");

        result.Text.Should().Be("This is the text.");
    }

    [Fact]
    public void Clean_keepsQuotesWhenTheInputWasQuoted()
    {
        var result = OutputCleaner.Clean("\"rough text\"", "\"This is the text.\"");

        result.Text.Should().Be("\"This is the text.\"");
    }

    // Rule 4
    [Theory]
    [InlineData("This is the text.\n\nNote: I kept the meaning.")]
    [InlineData("This is the text.\nExplanation: grammar only.")]
    [InlineData("This is the text.\nI corrected the spelling.")]
    [InlineData("This is the text.\nChanges: fixed \"teh\".")]
    public void Clean_removesTrailingNotes(string output)
    {
        var result = OutputCleaner.Clean(Input, output);

        result.Text.Should().Be("This is the text.");
    }

    [Fact]
    public void Clean_keepsANoteThatIsNotAtTheEnd()
    {
        const string output = "Note: this is part of the rewrite.\n\nAnd this continues.";

        var result = OutputCleaner.Clean(Input, output);

        result.Text.Should().Be(output);
    }

    // Rule 5
    [Fact]
    public void Clean_keepsTheInputsTrailingNewline()
    {
        var result = OutputCleaner.Clean("line one\n", "line one rewritten\n");

        result.Text.Should().Be("line one rewritten\n");
    }

    [Fact]
    public void Clean_trimsSurroundingWhitespace()
    {
        var result = OutputCleaner.Clean(Input, "\n\n  This is the text.  \n\n");

        result.Text.Should().Be("This is the text.");
    }

    // Rule 6
    [Fact]
    public void Clean_rejectsAnEmptyAnswer()
    {
        var result = OutputCleaner.Clean(Input, "   \n  ");

        result.Accepted.Should().BeFalse();
        result.Rejection.Should().Be(OutputRejection.Empty);
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public void Clean_rejectsOutputThatRanAway()
    {
        var output = new string('x', (4 * Input.Length) + 201);

        var result = OutputCleaner.Clean(Input, output);

        result.Accepted.Should().BeFalse();
        result.Rejection.Should().Be(OutputRejection.Runaway);
    }

    [Fact]
    public void Clean_acceptsOutputAtTheRunawayLimit()
    {
        var output = new string('x', (4 * Input.Length) + 200);

        var result = OutputCleaner.Clean(Input, output);

        result.Accepted.Should().BeTrue();
    }
}
