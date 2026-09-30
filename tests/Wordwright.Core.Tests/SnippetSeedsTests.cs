using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class SnippetSeedsTests
{
    [Fact]
    public void Default_offersTheThreeExamplesFromThePlan()
    {
        var document = SnippetSeeds.Default();

        document.Snippets.Select(snippet => snippet.Trigger).Should().Equal("date", "thanks", "sig");
    }

    [Fact]
    public void Default_seedsValidUsableSnippets()
    {
        var document = SnippetSeeds.Default();

        document.TriggerPrefix.Should().Be(";");
        document.Snippets.Should().OnlyContain(snippet =>
            SnippetRules.IsValidTrigger(snippet.Trigger)
            && snippet.Enabled
            && snippet.Name.Length > 0
            && snippet.Body.Length > 0);
        document.Snippets.Select(snippet => snippet.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Default_dateSnippetUsesTheDateVariable()
    {
        SnippetSeeds.Default().Snippets
            .Single(snippet => snippet.Trigger == "date")
            .Body.Should().Be("{date}");
    }

    [Fact]
    public void LoadOrSeed_withNoFile_writesTheExamplesOnce()
    {
        using var folder = new TemporaryFolder();
        var store = new SnippetStore(folder.Path);

        var seeded = store.LoadOrSeed();
        var loaded = store.Load();

        seeded.Snippets.Should().HaveCount(3);
        loaded.Snippets.Select(snippet => snippet.Trigger).Should().Equal("date", "thanks", "sig");
    }

    [Fact]
    public void LoadOrSeed_withAnExistingFile_keepsTheUsersSnippets()
    {
        using var folder = new TemporaryFolder();
        var store = new SnippetStore(folder.Path);
        store.Save(new SnippetDocument { Snippets = [new Snippet { Id = "a", Trigger = "mine", Name = "Mine" }] });

        var document = store.LoadOrSeed();

        document.Snippets.Should().ContainSingle().Which.Trigger.Should().Be("mine");
    }

    [Fact]
    public void LoadOrSeed_afterTheExamplesWereDeleted_doesNotBringThemBack()
    {
        using var folder = new TemporaryFolder();
        var store = new SnippetStore(folder.Path);
        _ = store.LoadOrSeed();
        store.Save(new SnippetDocument());

        store.LoadOrSeed().Snippets.Should().BeEmpty();
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
        }
    }
}
