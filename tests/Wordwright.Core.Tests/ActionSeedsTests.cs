using FluentAssertions;
using Wordwright.Core.Actions;

namespace Wordwright.Core.Tests;

public class ActionSeedsTests
{
    [Fact]
    public void BuiltIns_matchTheCopyInUxCopy()
    {
        var actions = ActionSeeds.BuiltIns();

        actions.Select(action => action.Id).Should().Equal(
            "fix", "clear", "formal", "friendly", "shorten", "custom");
    }

    [Fact]
    public void BuiltIns_haveTheLettersAndHotkeyFromTheCopy()
    {
        var actions = ActionSeeds.BuiltIns().ToDictionary(action => action.Id);

        actions["fix"].ShortcutKey.Should().Be("G");
        actions["clear"].ShortcutKey.Should().Be("C");
        actions["formal"].ShortcutKey.Should().Be("F");
        actions["friendly"].ShortcutKey.Should().Be("R");
        actions["shorten"].ShortcutKey.Should().Be("S");
        actions["custom"].ShortcutKey.Should().Be("I");

        // Only fix has a hotkey by default; more would risk clashing with other apps.
        actions.Values.Where(action => action.Hotkey is not null)
            .Should().ContainSingle().Which.Id.Should().Be("fix");
    }

    [Fact]
    public void OnlyTheCustomActionHasNoInstruction()
    {
        ActionSeeds.BuiltIns()
            .Where(action => action.Instruction.Length == 0)
            .Should().ContainSingle().Which.Id.Should().Be("custom");
    }
}
