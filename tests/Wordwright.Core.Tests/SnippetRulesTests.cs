using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class SnippetRulesTests
{
    [Theory]
    [InlineData("s")]
    [InlineData("sig")]
    [InlineData("a1-_")]
    [InlineData("SIG")]
    [InlineData("12345678901234567890123456789012")] // exactly 32
    public void IsValidTrigger_acceptsLettersNumbersDashAndUnderscore(string trigger)
    {
        SnippetRules.IsValidTrigger(trigger).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("123456789012345678901234567890123")] // 33
    [InlineData("si g")]
    [InlineData("sig!")]
    [InlineData("sig.")]
    [InlineData("s;ig")]
    [InlineData("sig,")]
    [InlineData("sïg")]
    public void IsValidTrigger_rejectsAnythingElse(string? trigger)
    {
        SnippetRules.IsValidTrigger(trigger).Should().BeFalse();
    }

    [Theory]
    [InlineData(";")]
    [InlineData("//")]
    [InlineData("::")]
    [InlineData(":")]
    [InlineData("#!$")]
    [InlineData("\\")]
    public void IsValidPrefix_acceptsOneToThreeSymbols(string prefix)
    {
        SnippetRules.IsValidPrefix(prefix).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(";;;;")]
    [InlineData("a")]
    [InlineData("1")]
    [InlineData(";a")]
    [InlineData(" ")]
    [InlineData("; ")]
    [InlineData("-")]
    [InlineData("_")]
    [InlineData("\t")]
    [InlineData("é")]
    public void IsValidPrefix_rejectsEmptyLongLettersDigitsAndSpace(string? prefix)
    {
        SnippetRules.IsValidPrefix(prefix).Should().BeFalse();
    }

    [Fact]
    public void IsTriggerTaken_findsAnExactMatch()
    {
        Snippet[] snippets = [new() { Id = "a", Trigger = "sig" }];

        SnippetRules.IsTriggerTaken(snippets, "sig").Should().BeTrue();
    }

    [Fact]
    public void IsTriggerTaken_isCaseSensitive()
    {
        Snippet[] snippets = [new() { Id = "a", Trigger = "sig" }];

        SnippetRules.IsTriggerTaken(snippets, "SIG").Should().BeFalse();
        SnippetRules.IsTriggerTaken(snippets, "Sig").Should().BeFalse();
    }

    [Fact]
    public void IsTriggerTaken_ignoresTheSnippetBeingEdited()
    {
        Snippet[] snippets = [new() { Id = "a", Trigger = "sig" }];

        SnippetRules.IsTriggerTaken(snippets, "sig", exceptId: "a").Should().BeFalse();
        SnippetRules.IsTriggerTaken(snippets, "sig", exceptId: "b").Should().BeTrue();
    }

    [Fact]
    public void IsTriggerTaken_findsNoMatchInAnEmptyList()
    {
        SnippetRules.IsTriggerTaken([], "sig").Should().BeFalse();
    }

    [Fact]
    public void Expandable_keepsEnabledSnippetsWithUsableTriggers()
    {
        Snippet[] snippets =
        [
            new() { Id = "a", Trigger = "sig", Enabled = true },
            new() { Id = "b", Trigger = "off", Enabled = false },
            new() { Id = "c", Trigger = "not valid", Enabled = true },
            new() { Id = "d", Trigger = "", Enabled = true },
        ];

        SnippetRules.Expandable(snippets).Select(snippet => snippet.Id).Should().Equal("a");
    }
}