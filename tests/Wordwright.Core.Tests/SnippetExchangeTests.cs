using System.IO;
using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class SnippetExchangeTests : IDisposable
{
    private readonly string _directory;

    public SnippetExchangeTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string FilePath(string name) => Path.Combine(_directory, name);

    private static Snippet NewSnippet(string trigger, string name = "Name", string body = "Body") => new()
    {
        Id = Snippet.NewId(),
        Trigger = trigger,
        Name = name,
        Body = body,
    };

    private static SnippetDocument Library(params Snippet[] snippets) => new() { Snippets = snippets };

    [Fact]
    public void Export_thenImport_roundTripsTheLibrary()
    {
        var path = FilePath("export.json");
        var library = Library(NewSnippet("sig", "Email signature", "Best regards"));

        SnippetExchange.Export(path, library);
        var imported = SnippetExchange.Import(path);

        imported.Should().NotBeNull();
        imported!.Snippets.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(library.Snippets[0]);
    }

    [Fact]
    public void Export_writesReadableJson()
    {
        var path = FilePath("readable.json");

        SnippetExchange.Export(path, Library(NewSnippet("sig", "Sig", "Best regards,\nJaspreet")));

        var text = File.ReadAllText(path);
        text.Should().Contain("\"trigger\": \"sig\"");
        text.Should().Contain("Jaspreet");
    }

    [Fact]
    public void Import_withSomethingElse_returnsNullAndLeavesTheFileAlone()
    {
        var path = FilePath("notes.json");
        File.WriteAllText(path, "this is not JSON at all");

        SnippetExchange.Import(path).Should().BeNull();
        File.ReadAllText(path).Should().Be("this is not JSON at all");
        File.Exists(path + ".corrupt").Should().BeFalse("an imported file is not ours to rename");
    }

    [Fact]
    public void Import_withAMissingFile_returnsNull()
    {
        SnippetExchange.Import(FilePath("nowhere.json")).Should().BeNull();
    }

    [Fact]
    public void Merge_addsTheImportedSnippetsAndKeepsTheLibrary()
    {
        var target = Library(NewSnippet("sig", "Mine"));
        var imported = Library(NewSnippet("addr", "Office address"), NewSnippet("ty", "Thank you"));

        var result = SnippetExchange.Merge(target, imported);

        result.Added.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.Document.Snippets.Select(snippet => snippet.Trigger).Should().Equal("sig", "addr", "ty");
    }

    [Fact]
    public void Merge_skipsAShortcutTheLibraryAlreadyUses()
    {
        var target = Library(NewSnippet("sig", "Mine"));
        var imported = Library(NewSnippet("sig", "Theirs"), NewSnippet("ty", "Thank you"));

        var result = SnippetExchange.Merge(target, imported);

        result.Added.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Document.Snippets.Single(snippet => snippet.Trigger == "sig").Name.Should().Be("Mine");
    }

    [Fact]
    public void Merge_isCaseSensitiveLikeTheRules()
    {
        var target = Library(NewSnippet("sig", "Mine"));
        var imported = Library(NewSnippet("SIG", "Theirs"));

        var result = SnippetExchange.Merge(target, imported);

        result.Added.Should().Be(1);
        result.Document.Snippets.Select(snippet => snippet.Trigger).Should().Equal("sig", "SIG");
    }

    [Fact]
    public void Merge_skipsDuplicatesWithinTheImportedFile()
    {
        var imported = Library(NewSnippet("sig", "First"), NewSnippet("sig", "Second"));

        var result = SnippetExchange.Merge(new SnippetDocument(), imported);

        result.Added.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Document.Snippets.Single().Name.Should().Be("First");
    }

    [Fact]
    public void Merge_skipsSnippetsWithUnusableShortcuts()
    {
        var imported = Library(NewSnippet("not valid"), NewSnippet(""), NewSnippet("good"));

        var result = SnippetExchange.Merge(new SnippetDocument(), imported);

        result.Added.Should().Be(1);
        result.Skipped.Should().Be(2);
        result.Document.Snippets.Single().Trigger.Should().Be("good");
    }

    [Fact]
    public void Merge_givesEveryImportedSnippetAFreshId()
    {
        var shared = NewSnippet("sig", "Theirs");
        var imported = Library(shared with { Id = "fixed" });

        var result = SnippetExchange.Merge(Library(NewSnippet("other")), imported);

        var added = result.Document.Snippets.Single(snippet => snippet.Trigger == "sig");
        added.Id.Should().NotBe("fixed");
        added.Id.Should().HaveLength(8);
        result.Document.Snippets.Select(snippet => snippet.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Merge_keepsTheImportedTextExactlyAsItWas()
    {
        var imported = Library(NewSnippet("sig", "Their name", "Best regards,\nJaspreet"));

        var result = SnippetExchange.Merge(new SnippetDocument(), imported);

        result.Document.Snippets.Single().Body.Should().Be("Best regards,\nJaspreet");
        result.Document.Snippets.Single().Name.Should().Be("Their name");
    }

    [Fact]
    public void Merge_ofAnEmptyFileChangesNothing()
    {
        var target = Library(NewSnippet("sig"));

        var result = SnippetExchange.Merge(target, new SnippetDocument());

        result.Added.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Document.Should().BeEquivalentTo(target);
    }
}