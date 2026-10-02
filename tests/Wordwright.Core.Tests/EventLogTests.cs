using FluentAssertions;
using Wordwright.Core.Diagnostics;

namespace Wordwright.Core.Tests;

public sealed class EventLogTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 14, 30, 0, TimeSpan.FromHours(5.5));

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "wordwright-log-tests-" + Guid.NewGuid().ToString("N"));

    private EventLog Log(DateTimeOffset? now = null) => new(_directory, () => now ?? Now);

    private string TodaysFile => Path.Combine(_directory, "wordwright-20261003.log");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Write_appendsTheEventToTodaysFile()
    {
        Log().Write("started");
        Log().Write("hook reinstalled");

        File.ReadAllLines(TodaysFile).Should().Equal(
            "2026-10-03T14:30:00.000+05:30 started",
            "2026-10-03T14:30:00.000+05:30 hook reinstalled");
    }

    [Fact]
    public void Write_anException_recordsItsTypeButNeverItsMessage()
    {
        Exception thrown;
        try
        {
            throw new InvalidOperationException(
                "secret snippet text",
                new FormatException("more secret text"));
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        Log().Write("unhandled exception", thrown);

        var text = File.ReadAllText(TodaysFile);
        text.Should().Contain("unhandled exception");
        text.Should().Contain("System.InvalidOperationException");
        text.Should().Contain("System.FormatException");
        text.Should().Contain(nameof(Write_anException_recordsItsTypeButNeverItsMessage));
        text.Should().NotContain("secret");
    }

    [Fact]
    public void Prune_deletesDayFilesOlderThanSevenDaysAndNothingElse()
    {
        Directory.CreateDirectory(_directory);
        string[] kept =
        [
            "wordwright-20261003.log",
            "wordwright-20260927.log", // the seventh day back, still kept
            "notes.txt",
            "wordwright-garbage.log",
        ];
        string[] deleted = ["wordwright-20260926.log", "wordwright-20250101.log"];
        foreach (var name in kept.Concat(deleted))
        {
            File.WriteAllText(Path.Combine(_directory, name), "x");
        }

        Log().Prune();

        Directory.EnumerateFiles(_directory).Select(Path.GetFileName)
            .Should().BeEquivalentTo(kept);
    }

    [Fact]
    public void Prune_withNoFolderYet_doesNothing()
    {
        var act = () => Log().Prune();

        act.Should().NotThrow();
        Directory.Exists(_directory).Should().BeFalse();
    }

    [Fact]
    public void Write_whereTheFolderCannotBeCreated_isDroppedQuietly()
    {
        // A file where the folder should be makes CreateDirectory fail.
        File.WriteAllText(_directory, "in the way");
        try
        {
            var act = () => new EventLog(_directory, () => Now).Write("started");

            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(_directory);
        }
    }
}
