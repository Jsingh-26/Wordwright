using System.Text;
using FluentAssertions;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

public class ModelImporterTests : IDisposable
{
    private readonly string _directory;
    private readonly string _models;
    private readonly string _downloads;

    public ModelImporterTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
        _models = Path.Combine(_directory, "models");
        _downloads = Path.Combine(_directory, "downloads");
        Directory.CreateDirectory(_downloads);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A file that starts with the GGUF magic and then holds noise.</summary>
    private string GgufFile(string name, int size = 2048)
    {
        var path = Path.Combine(_downloads, name);
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("GGUF").CopyTo(bytes, 0);

        for (var index = 4; index < size; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static ModelCatalog CatalogWith(params (string Id, string Path)[] models)
    {
        var entries = models.Select(model => new CatalogEntry
        {
            Id = model.Id,
            Sha256 = ModelDownloader.Hash(model.Path),
            Status = "approved",
            Tiers = ["cpu8"],
        }).ToList();

        return new ModelCatalog { SchemaVersion = 1, CatalogVersion = "2026-10-01", Models = entries };
    }

    [Fact]
    public void AFileWithoutTheMagic_isNotAModel()
    {
        var path = Path.Combine(_downloads, "notes.txt");
        File.WriteAllText(path, "this is not a model");

        ModelImporter.IsGguf(path).Should().BeFalse();
        ModelImporter.Import(path, _models, CatalogWith()).Outcome.Should().Be(ImportOutcome.NotGguf);
    }

    [Fact]
    public void AShortFile_isNotAModel()
    {
        var path = Path.Combine(_downloads, "tiny.gguf");
        File.WriteAllBytes(path, "GG"u8.ToArray());

        ModelImporter.IsGguf(path).Should().BeFalse();
    }

    [Fact]
    public void ARealGguf_isImportedIntoTheModelsFolder()
    {
        var path = GgufFile("mymodel.gguf");

        var (outcome, model, entry) = ModelImporter.Import(path, _models, CatalogWith());

        outcome.Should().Be(ImportOutcome.Imported);
        model!.Id.Should().Be("mymodel");
        model.File.Should().Be("mymodel.gguf");
        entry.Should().BeNull("the catalog does not know this file");
        model.Verified.Should().BeFalse("an imported file is taken on trust, so it is unverified");
        File.Exists(Path.Combine(_models, "mymodel.gguf")).Should().BeTrue();
        File.Exists(path).Should().BeFalse("the file moves in, so the app owns it");
    }

    [Fact]
    public void AFileTheCatalogKnows_countsAsThatEntry()
    {
        var path = GgufFile("mystery.gguf");
        var catalog = CatalogWith(("qwen3.5-2b-q4km", path));

        var (outcome, model, entry) = ModelImporter.Import(path, _models, catalog);

        outcome.Should().Be(ImportOutcome.Imported);
        model!.Id.Should().Be("qwen3.5-2b-q4km", "the hash names the model, not the file");
        model.Verified.Should().BeTrue();
        entry!.Id.Should().Be("qwen3.5-2b-q4km");
    }

    [Fact]
    public void AnEntryWithNoHash_neverMatches()
    {
        // The draft catalog's entries have no hashes yet; they must not claim a
        // file just because both are empty.
        var path = GgufFile("mystery.gguf");
        var catalog = new ModelCatalog
        {
            SchemaVersion = 1,
            CatalogVersion = "2026-10-01",
            Models = [new CatalogEntry { Id = "unfilled", Sha256 = "", Tiers = ["cpu8"] }],
        };

        var (_, model, entry) = ModelImporter.Import(path, _models, catalog);

        model!.Verified.Should().BeFalse();
        entry.Should().BeNull();
    }

    [Fact]
    public void ACustomId_isMadeSafeForAFileName()
    {
        var path = GgufFile("My Model (Q4_K_M) final.gguf");

        var (_, model, _) = ModelImporter.Import(path, _models, CatalogWith());

        model!.Id.Should().Be("My-Model--Q4_K_M--final");
    }

    [Fact]
    public void AMissingFile_failsQuietly()
    {
        var outcome = ModelImporter.Import(
            Path.Combine(_downloads, "gone.gguf"), _models, CatalogWith()).Outcome;

        outcome.Should().Be(ImportOutcome.Failed);
    }

    [Fact]
    public void TheRegistry_remembersWhatWasImported()
    {
        var path = GgufFile("mymodel.gguf");
        var (_, model, _) = ModelImporter.Import(path, _models, CatalogWith());

        var store = new InstalledModelStore(_models);
        var registry = store.Add(model!);

        registry.Models.Should().HaveCount(1);

        var reloaded = new InstalledModelStore(_models).Load();
        reloaded.Models.Should().HaveCount(1);
        reloaded.Models[0].Id.Should().Be("mymodel");
        reloaded.Models[0].Verified.Should().BeFalse();
        reloaded.Models[0].Calibration.Should().BeNull("nothing has measured it yet");
        store.PathOf(reloaded.Models[0]).Should().Be(Path.Combine(_models, "mymodel.gguf"));
    }

    [Fact]
    public void ImportingTheSameModelAgain_replacesItsRecord()
    {
        var store = new InstalledModelStore(_models);

        store.Add(new InstalledModel { Id = "mymodel", File = "a.gguf", Sha256 = "aa", Verified = false });
        var registry = store.Add(new InstalledModel { Id = "mymodel", File = "b.gguf", Sha256 = "bb", Verified = true });

        registry.Models.Should().HaveCount(1);
        registry.Models[0].File.Should().Be("b.gguf");
    }

    [Fact]
    public void ACorruptRegistry_isRenamedAndStartsEmpty()
    {
        Directory.CreateDirectory(_models);
        var path = Path.Combine(_models, "installed-models.json");
        File.WriteAllText(path, "{ not json at all");

        var registry = new InstalledModelStore(_models).Load();

        registry.Models.Should().BeEmpty();
        File.Exists(path + ".corrupt").Should().BeTrue();
    }
}
