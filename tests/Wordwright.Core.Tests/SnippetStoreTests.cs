using System.IO;
using System.Text.Json;
using FluentAssertions;
using Wordwright.Core.Snippets;

namespace Wordwright.Core.Tests;

public class SnippetStoreTests : IDisposable
{
    private readonly string _directory;

    public SnippetStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SnippetStore CreateStore() => new(_directory);

    private string SnippetsPath => Path.Combine(_directory, "snippets.json");

    private string BackupPath => SnippetsPath + ".bak";

    private string CorruptPath => SnippetsPath + ".corrupt";

    private static SnippetDocument Document(params Snippet[] snippets) =>
        new() { Snippets = snippets };

    private static Snippet Signature => new()
    {
        Id = "b1c2",
        Trigger = "sig",
        Name = "Email signature",
        Body = "Best regards,\nJaspreet",
        Enabled = true,
        CreatedUtc = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
        UpdatedUtc = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Load_withNoFile_returnsDefaults()
    {
        var document = CreateStore().Load();

        document.SchemaVersion.Should().Be(1);
        document.TriggerPrefix.Should().Be(";");
        document.Snippets.Should().BeEmpty();
    }

    [Fact]
    public void Load_withEmptyDirectory_doesNotCreateAFile()
    {
        _ = CreateStore().Load();

        File.Exists(SnippetsPath).Should().BeFalse();
    }

    [Fact]
    public void Save_thenLoad_roundTripsEveryField()
    {
        var store = CreateStore();
        var saved = Document(Signature);

        store.Save(saved);
        var loaded = store.Load();

        loaded.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public void Save_writesTheDocumentedFileFormat()
    {
        CreateStore().Save(Document(Signature));

        var json = JsonDocument.Parse(File.ReadAllText(SnippetsPath)).RootElement;
        json.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        json.GetProperty("triggerPrefix").GetString().Should().Be(";");

        var snippet = json.GetProperty("snippets").EnumerateArray().Single();
        snippet.GetProperty("id").GetString().Should().Be("b1c2");
        snippet.GetProperty("trigger").GetString().Should().Be("sig");
        snippet.GetProperty("name").GetString().Should().Be("Email signature");
        snippet.GetProperty("body").GetString().Should().Be("Best regards,\nJaspreet");
        snippet.GetProperty("enabled").GetBoolean().Should().BeTrue();
        snippet.TryGetProperty("createdUtc", out _).Should().BeTrue();
        snippet.TryGetProperty("updatedUtc", out _).Should().BeTrue();
    }

    [Fact]
    public void Save_keepsThePreviousVersionAsBackup()
    {
        var store = CreateStore();
        var first = Document(Signature);
        var second = Document(Signature with { Id = "d3e4", Trigger = "sign", Name = "Signature" });

        store.Save(first);
        store.Save(second);

        store.Load().Snippets.Single().Trigger.Should().Be("sign");
        File.Exists(BackupPath).Should().BeTrue("the version being replaced is kept as .bak");
        JsonDocument.Parse(File.ReadAllText(BackupPath)).RootElement
            .GetProperty("snippets").EnumerateArray().Single()
            .GetProperty("trigger").GetString().Should().Be("sig");
    }

    [Fact]
    public void Save_doesNotLeaveATempFileBehind()
    {
        var store = CreateStore();
        store.Save(Document(Signature));
        store.Save(Document(Signature with { Name = "Signature" }));

        Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Save_createsTheDirectoryWhenMissing()
    {
        CreateStore().Save(Document(Signature));

        File.Exists(SnippetsPath).Should().BeTrue();
    }

    [Fact]
    public void Load_withCorruptFile_renamesItAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SnippetsPath, "{ not snippets }");

        var document = CreateStore().Load();

        document.Snippets.Should().BeEmpty();
        File.Exists(SnippetsPath).Should().BeFalse();
        File.ReadAllText(CorruptPath).Should().Be("{ not snippets }");
    }

    [Fact]
    public void Load_afterCorruptFile_canStillSave()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SnippetsPath, "not json");

        _ = store.Load();
        store.Save(Document(Signature));

        store.Load().Snippets.Should().ContainSingle();
    }

    [Fact]
    public void Load_toleratesSnippetsWithMissingFields()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SnippetsPath, """
            { "schemaVersion": 1, "triggerPrefix": ";", "snippets": [ { "trigger": "sig" } ] }
            """);

        var snippet = CreateStore().Load().Snippets.Single();

        snippet.Trigger.Should().Be("sig");
        snippet.Name.Should().BeEmpty();
        snippet.Enabled.Should().BeTrue();
    }

    [Fact]
    public void NewId_isShortAndUnique()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Snippet.NewId()).ToList();

        ids.Should().OnlyContain(id => id.Length == 8);
        ids.Should().OnlyHaveUniqueItems();
    }
}