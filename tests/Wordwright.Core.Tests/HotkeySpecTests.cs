using FluentAssertions;
using Wordwright.Core.Actions;

namespace Wordwright.Core.Tests;

public class HotkeySpecTests
{
    [Theory]
    [InlineData("Ctrl+Alt+G", HotkeyModifiers.Control | HotkeyModifiers.Alt, "G")]
    [InlineData("ctrl+shift+7", HotkeyModifiers.Control | HotkeyModifiers.Shift, "7")]
    [InlineData("Win+Space", HotkeyModifiers.Win, "Space")]
    [InlineData("Alt+F4", HotkeyModifiers.Alt, "F4")]
    public void Parse_readsACombination(string text, HotkeyModifiers modifiers, string key)
    {
        var spec = HotkeySpec.Parse(text);

        spec.Should().NotBeNull();
        spec!.Modifiers.Should().Be(modifiers);
        spec.Key.Should().Be(key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]              // key only, no modifier
    [InlineData("Ctrl")]           // modifier only
    [InlineData("Ctrl+")]          // nothing after the modifier
    [InlineData("Ctrl+Ctrl+G")]    // the same modifier twice
    [InlineData("Hyper+G")]        // unknown modifier
    [InlineData("Ctrl+F25")]       // function key out of range
    [InlineData("Ctrl++)")]        // punctuation is not accepted
    public void Parse_rejectsWhatIsNotAHotkey(string text)
    {
        HotkeySpec.Parse(text).Should().BeNull();
    }

    [Fact]
    public void ToString_writesAFixedModifierOrder()
    {
        var spec = new HotkeySpec
        {
            Modifiers = HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Control,
            Key = "K",
        };

        spec.ToString().Should().Be("Ctrl+Shift+Win+K");
    }

    [Fact]
    public void Parse_roundTripsThroughToString()
    {
        const string text = "Ctrl+Alt+Space";

        HotkeySpec.Parse(text)!.ToString().Should().Be(text);
    }

    [Fact]
    public void IsUsable_needsBothAModifierAndAKey()
    {
        new HotkeySpec { Modifiers = HotkeyModifiers.Control, Key = "G" }.IsUsable.Should().BeTrue();
        new HotkeySpec { Modifiers = HotkeyModifiers.None, Key = "G" }.IsUsable.Should().BeFalse();
        new HotkeySpec { Modifiers = HotkeyModifiers.Control, Key = "" }.IsUsable.Should().BeFalse();
    }
}

public class HotkeyRulesTests
{
    [Theory]
    [InlineData("Ctrl+Alt+G")]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Win+Shift+S")]
    public void Validate_acceptsAUsableCombination(string text)
    {
        HotkeyRules.Validate(text).Should().Be(HotkeyValidation.Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+F30")]
    public void Validate_flagsWhatIsInvalid(string text)
    {
        HotkeyRules.Validate(text).Should().Be(HotkeyValidation.Invalid);
    }

    [Theory]
    [InlineData("Win+L")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Alt+Tab")]
    [InlineData("Alt+F4")]
    public void Validate_flagsWhatWindowsReserves(string text)
    {
        HotkeyRules.Validate(text).Should().Be(HotkeyValidation.Reserved);
    }

    [Fact]
    public void IsDuplicate_findsAClashButHonoursTheException()
    {
        var palette = HotkeySpec.Parse("Ctrl+Alt+Space")!;
        var fix = HotkeySpec.Parse("Ctrl+Alt+G")!;
        var candidate = HotkeySpec.Parse("Ctrl+Alt+G")!;

        HotkeyRules.IsDuplicate([palette, fix], candidate).Should().BeTrue();
        HotkeyRules.IsDuplicate([palette, fix], candidate, except: fix).Should().BeFalse();
        HotkeyRules.IsDuplicate([palette, fix], HotkeySpec.Parse("Ctrl+Alt+H")!).Should().BeFalse();
    }
}
