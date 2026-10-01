using FluentAssertions;
using Wordwright.Core.Actions;

namespace Wordwright.Core.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Build_putsTheInstructionBeforeTheText()
    {
        var prompt = PromptBuilder.Build("Fix grammar.", "teh cat");

        prompt.System.Should().Be(PromptBuilder.SystemMessage);
        prompt.User.Should().Be("Instruction: Fix grammar.\n\nText:\nteh cat");
    }

    [Fact]
    public void SystemMessage_asksForABareRewrite()
    {
        PromptBuilder.SystemMessage.Should().Contain("no introduction, no quotes, no notes, no explanation");
        PromptBuilder.SystemMessage.Should().Contain("Keep the original language");
    }

    [Fact]
    public void MaxNewTokens_doublesTheInputAndAddsSixtyFour()
    {
        PromptBuilder.MaxNewTokens(100).Should().Be(264);
    }

    [Fact]
    public void MaxNewTokens_stopsAtTheCap()
    {
        PromptBuilder.MaxNewTokens(5000).Should().Be(PromptBuilder.MaxNewTokensCap);
    }
}
