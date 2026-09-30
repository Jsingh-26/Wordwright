using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class TriggerMatcherTests
{
    private const string Prefix = ";";

    private static Snippet Snippet(string trigger, string? body = null, bool enabled = true) => new()
    {
        Id = trigger,
        Trigger = trigger,
        Name = trigger,
        Body = body ?? $"body-{trigger}",
        Enabled = enabled,
    };

    private static TriggerMatcher Matcher(params Snippet[] snippets) => new(Prefix, snippets);

    private static TriggerMatcher Matcher(IEnumerable<Snippet> snippets) => new(Prefix, snippets);

    [Fact]
    public void TryMatch_expandsAFinishedTrigger()
    {
        var match = Matcher(Snippet("sig", "Best regards")).TryMatch(";sig");

        match.Should().NotBeNull();
        match!.Snippet.Trigger.Should().Be("sig");
        match.Text.Should().Be("Best regards");
        match.TypedLength.Should().Be(4);
        match.Delimiter.Should().BeEmpty();
    }

    [Theory]
    [InlineData(";sig", 4)]          // start of buffer
    [InlineData("hello ;sig", 4)]    // after a space
    [InlineData("hello. ;sig", 4)]   // after punctuation
    [InlineData("(;sig", 4)]         // after an opening bracket
    public void TryMatch_acceptsATriggerOnAWordBoundary(string buffer, int typedLength)
    {
        var match = Matcher(Snippet("sig")).TryMatch(buffer);

        match.Should().NotBeNull();
        match!.TypedLength.Should().Be(typedLength);
    }

    [Theory]
    [InlineData("a;sig")]
    [InlineData("hello;sig")]
    [InlineData("1;sig")]
    [InlineData("a_;sig")]           // underscore counts as part of a word
    public void TryMatch_ignoresATriggerGluedToAWord(string buffer)
    {
        Matcher(Snippet("sig")).TryMatch(buffer).Should().BeNull();
    }

    [Fact]
    public void TryMatch_isCaseSensitive()
    {
        var matcher = Matcher(Snippet("sig"));

        matcher.TryMatch(";SIG").Should().BeNull();
        matcher.TryMatch(";Sig").Should().BeNull();
    }

    [Fact]
    public void TryMatch_needsTheTriggerAtTheEndOfTheBuffer()
    {
        var matcher = Matcher(Snippet("sig"));

        matcher.TryMatch(";sigx").Should().BeNull();
        matcher.TryMatch(";sig").Should().NotBeNull();
    }

    [Fact]
    public void TryMatch_prefersTheLongestTrigger()
    {
        var match = Matcher(Snippet("s"), Snippet("sig")).TryMatch(";sig");

        match.Should().NotBeNull();
        match!.Snippet.Trigger.Should().Be("sig");
    }

    [Fact]
    public void TryMatch_waitsWhenOneTriggerBeginsAnother()
    {
        var matcher = Matcher(Snippet("s"), Snippet("sig"));

        matcher.TryMatch(";s").Should().BeNull("sig may still be on its way");
    }

    [Theory]
    [InlineData(";s ", " ")]
    [InlineData(";s.", ".")]
    [InlineData(";s,", ",")]
    [InlineData(";s!", "!")]
    public void TryMatch_expandsAnAmbiguousTriggerWhenADelimiterArrives(string buffer, string delimiter)
    {
        var match = Matcher(Snippet("s"), Snippet("sig")).TryMatch(buffer);

        match.Should().NotBeNull();
        match!.Snippet.Trigger.Should().Be("s");
        match.Delimiter.Should().Be(delimiter);
        match.TypedLength.Should().Be(3, "the prefix, the trigger and the delimiter are replaced");
    }

    [Fact]
    public void TryMatch_keepsWaitingWhenALetterFollowsAnAmbiguousTrigger()
    {
        Matcher(Snippet("s"), Snippet("sig")).TryMatch(";sx").Should().BeNull();
    }

    [Fact]
    public void TryMatch_expandsAnUnambiguousTriggerWithItsDelimiter()
    {
        var match = Matcher(Snippet("sig")).TryMatch(";sig ");

        match.Should().NotBeNull();
        match!.TypedLength.Should().Be(5);
        match.Delimiter.Should().Be(" ");
    }

    [Fact]
    public void TryMatch_waitsWhenALongerTriggerSharesTheShortOne()
    {
        var matcher = Matcher(Snippet("sig"), Snippet("sig2"));

        matcher.TryMatch(";sig").Should().BeNull();

        var match = matcher.TryMatch(";sig ");
        match.Should().NotBeNull();
        match!.Snippet.Trigger.Should().Be("sig");
        match.Delimiter.Should().Be(" ");
    }

    [Fact]
    public void TryMatch_ignoresDisabledAndInvalidSnippets()
    {
        var disabled = Matcher(Snippet("sig", enabled: false));
        var invalid = Matcher(Snippet("not valid"));

        disabled.TryMatch(";sig").Should().BeNull();
        invalid.TryMatch(";not valid").Should().BeNull();
    }

    [Fact]
    public void TryMatch_returnsTheSnippetWhoseTriggerMatched()
    {
        var matcher = Matcher(Snippet("date", "2026-10-01"), Snippet("thanks", "Thank you"));

        matcher.TryMatch(";date")!.Text.Should().Be("2026-10-01");
        matcher.TryMatch(";thanks")!.Text.Should().Be("Thank you");
    }

    [Fact]
    public void TryMatch_usesTheConfiguredPrefix()
    {
        var matcher = new TriggerMatcher("/", [Snippet("sig")]);

        matcher.TryMatch("/sig").Should().NotBeNull();
        matcher.TryMatch(";sig").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no trigger here")]
    [InlineData(";")]
    public void TryMatch_withoutATrigger_returnsNothing(string? buffer)
    {
        Matcher(Snippet("sig")).TryMatch(buffer).Should().BeNull();
    }

    [Fact]
    public void TryMatch_withoutSnippets_returnsNothing()
    {
        Matcher([]).TryMatch(";sig").Should().BeNull();
    }
}