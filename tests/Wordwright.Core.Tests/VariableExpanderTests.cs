using System.Globalization;
using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class VariableExpanderTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 10, 1, 13, 45, 0, TimeSpan.FromHours(2));

    private static VariableExpander Expander(string? clipboard = null) =>
        new(() => FixedNow, () => clipboard);

    [Fact]
    public void Expand_withNoVariables_leavesTheBodyAlone()
    {
        var result = Expander().Expand("Best regards,\nJaspreet");

        result.Text.Should().Be("Best regards,\nJaspreet");
        result.CharactersAfterCursor.Should().Be(0);
    }

    [Fact]
    public void Expand_replacesDateWithTheSystemShortDate()
    {
        var result = Expander().Expand("Today is {date}.");

        result.Text.Should().Be($"Today is {FixedNow.ToString("d", CultureInfo.CurrentCulture)}.");
    }

    [Fact]
    public void Expand_replacesTimeWithTheSystemShortTime()
    {
        var result = Expander().Expand("at {time}");

        result.Text.Should().Be($"at {FixedNow.ToString("t", CultureInfo.CurrentCulture)}");
    }

    [Fact]
    public void Expand_replacesClipboardWithItsText()
    {
        Expander("pasted text").Expand("[{clipboard}]").Text.Should().Be("[pasted text]");
    }

    [Fact]
    public void Expand_withNoClipboardText_insertsNothing()
    {
        Expander(null).Expand("[{clipboard}]").Text.Should().Be("[]");
    }

    [Fact]
    public void Expand_neverExpandsClipboardTextAgain()
    {
        Expander("{date}").Expand("{clipboard}").Text.Should().Be("{date}");
    }

    [Fact]
    public void Expand_dropsTheCursorMarkerAndCountsTheCharactersAfterIt()
    {
        var result = Expander().Expand("Dear {cursor},\nBest regards");

        result.Text.Should().Be("Dear ,\nBest regards");
        result.CharactersAfterCursor.Should().Be(14);
    }

    [Fact]
    public void Expand_withTheCursorMarkerAtTheEnd_leavesTheCaretThere()
    {
        var result = Expander().Expand("Best regards{cursor}");

        result.Text.Should().Be("Best regards");
        result.CharactersAfterCursor.Should().Be(0);
    }

    [Fact]
    public void Expand_countsCharactersAfterTheCursorInTheExpandedText()
    {
        var result = Expander().Expand("{cursor}{date}");

        result.Text.Should().Be(FixedNow.ToString("d", CultureInfo.CurrentCulture));
        result.CharactersAfterCursor.Should().Be(result.Text.Length);
    }

    [Fact]
    public void Expand_usesTheFirstCursorMarkerAndDropsTheRest()
    {
        var result = Expander().Expand("a{cursor}b{cursor}c");

        result.Text.Should().Be("abc");
        result.CharactersAfterCursor.Should().Be(2);
    }

    [Fact]
    public void Expand_leavesUnknownVariablesAsTyped()
    {
        Expander().Expand("{foo} {bar} {}").Text.Should().Be("{foo} {bar} {}");
    }

    [Fact]
    public void Expand_treatsDoubleBracesAsALiteralBrace()
    {
        // Only "{{" is special (docs/ARCHITECTURE.md), so the rest stays as typed
        // and "{{date}" is how a body shows a literal "{date}".
        Expander().Expand("{{").Text.Should().Be("{");
        Expander().Expand("{{date}}").Text.Should().Be("{date}}");
        Expander().Expand("{{date}").Text.Should().Be("{date}");
    }

    [Fact]
    public void Expand_keepsAnUnclosedBrace()
    {
        Expander().Expand("a { b").Text.Should().Be("a { b");
    }

    [Fact]
    public void Expand_isCaseSensitive()
    {
        Expander("clip").Expand("{Date} {CLIPBOARD}").Text.Should().Be("{Date} {CLIPBOARD}");
    }

    [Fact]
    public void Expand_mixesVariablesAndPlainText()
    {
        var result = Expander("sig").Expand("{date} at {time}: {clipboard}{cursor}!");

        result.Text.Should().Be(
            $"{FixedNow.ToString("d", CultureInfo.CurrentCulture)} at " +
            $"{FixedNow.ToString("t", CultureInfo.CurrentCulture)}: sig!");
        result.CharactersAfterCursor.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Expand_withAnEmptyBody_returnsNothing(string? body)
    {
        var result = Expander().Expand(body);

        result.Text.Should().BeEmpty();
        result.CharactersAfterCursor.Should().Be(0);
    }
}
/// <summary>The Insert buttons and the expander must agree on every token.</summary>
public class SnippetVariablesTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 10, 1, 13, 45, 0, TimeSpan.FromHours(2));

    [Fact]
    public void EachTokenIsOneTheExpanderUnderstands()
    {
        var expander = new VariableExpander(() => FixedNow, () => "copied");

        expander.Expand(SnippetVariables.Date).Text.Should().NotBe(SnippetVariables.Date);
        expander.Expand(SnippetVariables.Time).Text.Should().NotBe(SnippetVariables.Time);
        expander.Expand(SnippetVariables.Clipboard).Text.Should().Be("copied");
        expander.Expand(SnippetVariables.Cursor).Text.Should().BeEmpty();
    }

    [Fact]
    public void TheCursorTokenIsRecognisedAsACursorMarker()
    {
        var expander = new VariableExpander(() => FixedNow, () => null);

        var result = expander.Expand(SnippetVariables.Cursor + "abc");

        result.Text.Should().Be("abc");
        result.CharactersAfterCursor.Should().Be(3);
    }
}
