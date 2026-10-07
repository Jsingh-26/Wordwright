using System.IO;
using FluentAssertions;
using Wordwright.Core.Storage;

namespace Wordwright.Core.Tests;

public class DataFolderImportTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _destination;

    public DataFolderImportTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Wordwright.Tests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(_source);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteSource(string name, string text) => File.WriteAllText(Path.Combine(_source, name), text);

    [Fact]
    public void CopyIfEmpty_withNoDestination_copiesBothFiles()
    {
        WriteSource("settings.json", "settings");
        WriteSource("snippets.json", "snippets");

        DataFolderImport.CopyIfEmpty(_source, _destination).Should().BeTrue();

        File.ReadAllText(Path.Combine(_destination, "settings.json")).Should().Be("settings");
        File.ReadAllText(Path.Combine(_destination, "snippets.json")).Should().Be("snippets");
    }

    [Fact]
    public void CopyIfEmpty_leavesTheSourceInPlace()
    {
        WriteSource("snippets.json", "snippets");

        DataFolderImport.CopyIfEmpty(_source, _destination);

        File.Exists(Path.Combine(_source, "snippets.json")).Should().BeTrue();
    }

    [Fact]
    public void CopyIfEmpty_copiesOnlyTheDataFiles()
    {
        WriteSource("snippets.json", "snippets");
        WriteSource("snippets.json.bak", "old");
        Directory.CreateDirectory(Path.Combine(_source, "logs"));

        DataFolderImport.CopyIfEmpty(_source, _destination);

        Directory.GetFileSystemEntries(_destination).Select(Path.GetFileName).Should().Equal("snippets.json");
    }

    [Fact]
    public void CopyIfEmpty_whenTheDestinationHasData_copiesNothing()
    {
        WriteSource("settings.json", "installer settings");
        WriteSource("snippets.json", "installer snippets");
        Directory.CreateDirectory(_destination);
        File.WriteAllText(Path.Combine(_destination, "snippets.json"), "store snippets");

        DataFolderImport.CopyIfEmpty(_source, _destination).Should().BeFalse();

        File.ReadAllText(Path.Combine(_destination, "snippets.json")).Should().Be("store snippets");
        File.Exists(Path.Combine(_destination, "settings.json")).Should().BeFalse();
    }

    [Fact]
    public void CopyIfEmpty_withNoSource_copiesNothingAndCreatesNothing()
    {
        DataFolderImport.CopyIfEmpty(Path.Combine(_root, "missing"), _destination).Should().BeFalse();

        Directory.Exists(_destination).Should().BeFalse();
    }
}
