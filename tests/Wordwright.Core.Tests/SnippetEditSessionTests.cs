using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class SnippetEditSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Start;
    private int _writes;

    private SnippetDocument _document = new()
    {
        Snippets =
        [
            new Snippet { Id = "a", Trigger = "sig", Name = "Signature", Body = "Best," },
            new Snippet { Id = "b", Trigger = "addr", Name = "Address", Body = "1 Main St" },
        ],
    };

    private SnippetEditSession NewSession() =>
        new(() => _now, () => _document, document =>
        {
            _document = document;
            _writes++;
        });

    private Snippet Stored(string id) => _document.Snippets.Single(snippet => snippet.Id == id);

    [Fact]
    public void Edit_isWrittenOnlyOnceTheTypingPauses()
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Edit("Sign-off", "sig", "Thanks,");
        _now += TimeSpan.FromMilliseconds(599);
        session.SaveIfDue().Should().BeNull();
        _writes.Should().Be(0);

        _now += TimeSpan.FromMilliseconds(1);
        var saved = session.SaveIfDue();

        saved!.Name.Should().Be("Sign-off");
        Stored("a").Body.Should().Be("Thanks,");
        Stored("a").UpdatedUtc.Should().Be(_now);
        session.IsPending.Should().BeFalse();
    }

    [Fact]
    public void AnotherEdit_pushesTheWriteBack()
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Edit("S", "sig", "Best,");
        _now += TimeSpan.FromMilliseconds(400);
        session.Edit("Si", "sig", "Best,");
        _now += TimeSpan.FromMilliseconds(400);

        session.SaveIfDue().Should().BeNull();
        session.DueUtc.Should().Be(Start + TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void Open_writesTheOutgoingSnippetsEditsFirst()
    {
        var session = NewSession();
        session.Open(Stored("a"));
        session.Edit("Signature", "sig", "Kind regards,");

        var saved = session.Open(Stored("b"));

        saved!.Id.Should().Be("a");
        Stored("a").Body.Should().Be("Kind regards,");
        Stored("b").Body.Should().Be("1 Main St");
        session.Snippet!.Id.Should().Be("b");
        session.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Flush_writesWhatIsWaiting_forExample_onExit()
    {
        var session = NewSession();
        session.Open(Stored("a"));
        session.Edit("Signature", "sig", "Cheers,");

        session.Flush().Should().NotBeNull();
        Stored("a").Body.Should().Be("Cheers,");

        session.Flush().Should().BeNull();
        _writes.Should().Be(1);
    }

    [Fact]
    public void Flush_withNothingWaiting_writesNothing()
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Flush().Should().BeNull();
        session.Open(Stored("b")).Should().BeNull();
        _writes.Should().Be(0);
    }

    [Theory]
    [InlineData("si g")] // invalid
    [InlineData("addr")] // taken by another snippet
    public void UnusableShortcut_keepsTheStoredTrigger_butSavesNameAndText(string shortcut)
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Edit("Renamed", shortcut, "New text");
        session.Flush();

        Stored("a").Trigger.Should().Be("sig");
        Stored("a").Name.Should().Be("Renamed");
        Stored("a").Body.Should().Be("New text");
    }

    [Fact]
    public void UnusableShortcut_keepsTheTriggerAcrossLaterSaves()
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Edit("Signature", "si g", "Best,");
        session.Flush();
        session.Edit("Signature", "si g", "Best wishes,");
        session.Flush();

        Stored("a").Trigger.Should().Be("sig");
        Stored("a").Body.Should().Be("Best wishes,");
    }

    [Fact]
    public void UsableShortcut_isWritten_andAnEmptyOneIsAWorkInProgress()
    {
        var session = NewSession();
        session.Open(Stored("a"));

        session.Edit("Signature", "sig2", "Best,");
        session.Flush();
        Stored("a").Trigger.Should().Be("sig2");

        session.Edit("Signature", "", "Best,");
        session.Flush();
        Stored("a").Trigger.Should().BeEmpty();
    }

    [Fact]
    public void Discard_dropsTheWaitingEdits()
    {
        var session = NewSession();
        session.Open(Stored("a"));
        session.Edit("Gone", "sig", "Gone");

        session.Discard();

        session.Flush().Should().BeNull();
        _now += TimeSpan.FromSeconds(1);
        session.SaveIfDue().Should().BeNull();
        Stored("a").Name.Should().Be("Signature");
    }

    [Fact]
    public void Edit_withNoSnippetOpen_doesNothing()
    {
        var session = NewSession();
        session.Open(null);

        session.Edit("x", "x", "x");

        session.IsPending.Should().BeFalse();
        session.Flush().Should().BeNull();
    }
}
