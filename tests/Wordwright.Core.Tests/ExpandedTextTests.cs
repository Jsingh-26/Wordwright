using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class ExpandedTextTests
{
    private static ExpandedText Text(string text, int afterCursor = 0) =>
        new() { Text = text, CharactersAfterCursor = afterCursor };

    [Theory]
    [InlineData("Best regards,\nJaspreet", "Best regards,\r\nJaspreet")]
    [InlineData("a\r\nb", "a\r\nb")]
    [InlineData("a\rb", "a\r\nb")]
    [InlineData("a\n\r\n\rb\n", "a\r\n\r\n\r\nb\r\n")]
    [InlineData("no breaks", "no breaks")]
    [InlineData("", "")]
    public void WithWindowsLineEndings_makesEveryBreakCrLf(string text, string expected)
    {
        Text(text).WithWindowsLineEndings().Text.Should().Be(expected);
    }

    [Fact]
    public void WithWindowsLineEndings_withNoCursor_keepsZeroArrows()
    {
        Text("a\nb").WithWindowsLineEndings().CharactersAfterCursor.Should().Be(0);
    }

    [Fact]
    public void WithWindowsLineEndings_countsEachBreakAfterTheCursorOnce()
    {
        // "Dear |,\nRegards": the caret sits before ",\nRegards" (10 characters).
        var result = Text("Dear ,\nRegards", afterCursor: 10).WithWindowsLineEndings();

        result.Text.Should().Be("Dear ,\r\nRegards");
        result.CharactersAfterCursor.Should().Be(10);
    }

    [Fact]
    public void WithWindowsLineEndings_fixesTheCountForBodiesAlreadyInCrLf()
    {
        // From the editor: ",\r\nRegards" is 11 characters but 10 caret steps.
        var result = Text("Dear ,\r\nRegards", afterCursor: 11).WithWindowsLineEndings();

        result.CharactersAfterCursor.Should().Be(10);
    }

    [Fact]
    public void WithWindowsLineEndings_leavesBreaksBeforeTheCursorOutOfTheCount()
    {
        var result = Text("Hi\nthere\n!", afterCursor: 1).WithWindowsLineEndings();

        result.Text.Should().Be("Hi\r\nthere\r\n!");
        result.CharactersAfterCursor.Should().Be(1);
    }
}
