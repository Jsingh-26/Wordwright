using FluentAssertions;
using Wordwright.Core.Keystrokes;

namespace Wordwright.Core.Tests;

public class KeystrokeBufferTests
{
    [Fact]
    public void Append_collectsTypedCharacters()
    {
        var buffer = new KeystrokeBuffer();

        foreach (var character in ";sig")
        {
            buffer.Append(character);
        }

        buffer.Text.Should().Be(";sig");
        buffer.Length.Should().Be(4);
    }

    [Fact]
    public void Append_keepsOnlyTheLastSixtyFourCharacters()
    {
        var buffer = new KeystrokeBuffer();

        for (var index = 0; index < 100; index++)
        {
            buffer.Append((char)('a' + index % 26));
        }

        var expected = string.Concat(
            Enumerable.Range(36, KeystrokeBuffer.MaxLength).Select(index => (char)('a' + index % 26)));

        buffer.Length.Should().Be(KeystrokeBuffer.MaxLength);
        buffer.Text.Should().Be(expected, "the oldest characters are dropped first");
    }

    [Fact]
    public void Append_atExactlyTheLimit_keepsTheBufferFull()
    {
        var buffer = new KeystrokeBuffer();

        for (var index = 0; index < KeystrokeBuffer.MaxLength; index++)
        {
            buffer.Append('x');
        }

        buffer.Length.Should().Be(KeystrokeBuffer.MaxLength);
        buffer.Append('y');

        buffer.Length.Should().Be(KeystrokeBuffer.MaxLength);
        buffer.Text.Should().Be(new string('x', KeystrokeBuffer.MaxLength - 1) + "y");
    }

    [Fact]
    public void Backspace_removesTheLastCharacter()
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append('a');
        buffer.Append('b');

        buffer.Backspace();

        buffer.Text.Should().Be("a");
    }

    [Fact]
    public void Backspace_onAnEmptyBuffer_doesNothing()
    {
        var buffer = new KeystrokeBuffer();

        buffer.Backspace();

        buffer.Text.Should().BeEmpty();
    }

    [Fact]
    public void RemoveLast_dropsTheCharactersJustTyped()
    {
        var buffer = new KeystrokeBuffer();
        foreach (var character in "hello ;sig")
        {
            buffer.Append(character);
        }

        buffer.RemoveLast(4);          // the trigger that was just replaced

        buffer.Text.Should().Be("hello ");
    }

    [Fact]
    public void RemoveLast_neverGoesBelowEmpty()
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append('a');

        buffer.RemoveLast(10);

        buffer.Text.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void RemoveLast_withNothingToRemove_leavesTheBufferAlone(int count)
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append('a');
        buffer.Append('b');

        buffer.RemoveLast(count);

        buffer.Text.Should().Be("ab");
    }

    [Fact]
    public void Clear_forgetsEverything()
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append('a');
        buffer.Append('b');

        buffer.Clear();

        buffer.Text.Should().BeEmpty();
        buffer.Length.Should().Be(0);
    }

    [Fact]
    public void Append_withinTheTypingTimeout_keepsWhatWasTypedBeforeThePause()
    {
        var clock = new TestClock();
        var buffer = new KeystrokeBuffer(() => clock.Now);

        Type(buffer, ";si");
        clock.Advance(TimeSpan.FromMilliseconds(4900));
        buffer.Append('g');

        buffer.Text.Should().Be(";sig", "4.9 s is still a pause a person typing makes");
    }

    [Fact]
    public void Append_afterTheTypingTimeout_startsAFreshBuffer()
    {
        var clock = new TestClock();
        var buffer = new KeystrokeBuffer(() => clock.Now);

        Type(buffer, ";si");
        clock.Advance(TimeSpan.FromMilliseconds(5100));
        buffer.Append('g');

        // The shortcut was not typed as a shortcut, so nothing expands.
        buffer.Text.Should().Be("g");
    }

    [Fact]
    public void TheTypingTimeoutIsMeasuredBetweenConsecutiveCharacters()
    {
        var clock = new TestClock();
        var buffer = new KeystrokeBuffer(() => clock.Now);

        Type(buffer, ";sig");
        clock.Advance(TimeSpan.FromSeconds(4));
        buffer.Append('!');
        clock.Advance(TimeSpan.FromSeconds(4));
        buffer.Append('?');

        buffer.Text.Should().Be(";sig!?", "each character restarts the five seconds");
    }

    [Fact]
    public void Append_afterAClearedBuffer_isUnaffectedByTheOldPause()
    {
        var clock = new TestClock();
        var buffer = new KeystrokeBuffer(() => clock.Now);

        Type(buffer, ";sig");
        clock.Advance(TimeSpan.FromMinutes(1));
        buffer.Clear();
        buffer.Append('g');

        buffer.Text.Should().Be("g");
    }

    [Fact]
    public void Backspace_afterTheTypingTimeout_removesNothingFromAFreshBuffer()
    {
        var clock = new TestClock();
        var buffer = new KeystrokeBuffer(() => clock.Now);

        Type(buffer, "abc");
        clock.Advance(TimeSpan.FromSeconds(6));
        buffer.Append('d');
        buffer.Backspace();

        buffer.Text.Should().BeEmpty("the pause already started the buffer over at 'd'");
    }

    private static void Type(KeystrokeBuffer buffer, string text)
    {
        foreach (var character in text)
        {
            buffer.Append(character);
        }
    }

    /// <summary>A clock the tests can move forward by hand.</summary>
    private sealed class TestClock
    {
        private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Now => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}