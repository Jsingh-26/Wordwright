using System.IO;
using System.Text.Json;
using FluentAssertions;
using Wordwright.Core.Settings;

namespace Wordwright.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory;

    public SettingsStoreTests()
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

    private SettingsStore CreateStore() => new(_directory);

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    private string CorruptPath => SettingsPath + ".corrupt";

    [Fact]
    public void Load_withNoFile_returnsDefaults()
    {
        var settings = CreateStore().Load();

        settings.SchemaVersion.Should().Be(1);
        settings.PaletteHotkey.Should().Be("Ctrl+Alt+Space");
        settings.SnippetsEnabled.Should().BeTrue();
        settings.StartWithWindows.Should().BeTrue();
        settings.AiEnabled.Should().BeFalse();
        settings.ActiveModelId.Should().BeNull();
        settings.UnloadAfterIdleMinutes.Should().Be(10);
        settings.CheckForBetterModelsWeekly.Should().BeFalse();
        settings.CheckForAppUpdatesWeekly.Should().BeFalse();
        settings.LastCatalogCheckUtc.Should().BeNull();
        settings.ExcludedApps.Should().Equal("KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe");
        settings.Theme.Should().Be("system");
        settings.Window.Should().BeNull("the window has not been closed yet on a first run");
    }

    [Fact]
    public void Load_withEmptyDirectory_doesNotCreateAFile()
    {
        _ = CreateStore().Load();

        File.Exists(SettingsPath).Should().BeFalse();
    }

    [Fact]
    public void Save_thenLoad_roundTripsEveryField()
    {
        var store = CreateStore();
        var saved = new AppSettings
        {
            PaletteHotkey = "Ctrl+Alt+W",
            SnippetsEnabled = false,
            StartWithWindows = false,
            AiEnabled = true,
            ActiveModelId = "qwen2.5-3b-instruct-q4",
            UnloadAfterIdleMinutes = 25,
            CheckForBetterModelsWeekly = true,
            CheckForAppUpdatesWeekly = true,
            LastCatalogCheckUtc = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero),
            ExcludedApps = ["KeePass.exe", "vault.exe"],
            Theme = "system",
            Window = new WindowPlacement
            {
                Left = 140,
                Top = 60,
                Width = 1200,
                Height = 800,
                Maximized = true,
            },
        };

        store.Save(saved);
        var loaded = store.Load();

        // BeEquivalentTo, not Be: records can't value-compare the ExcludedApps collection.
        loaded.Should().BeEquivalentTo(saved);
        loaded.Window.Should().Be(saved.Window);
    }

    [Fact]
    public void Save_writesTheWindowPlacementAsTheDocumentedWindowObject()
    {
        CreateStore().Save(new AppSettings
        {
            Window = new WindowPlacement { Left = 100, Top = 50, Maximized = false },
        });

        var window = JsonDocument.Parse(File.ReadAllText(SettingsPath)).RootElement.GetProperty("window");

        window.GetProperty("left").GetDouble().Should().Be(100);
        window.GetProperty("top").GetDouble().Should().Be(50);
        window.GetProperty("width").GetDouble().Should().Be(WindowPlacement.DefaultWidth);
        window.GetProperty("height").GetDouble().Should().Be(WindowPlacement.DefaultHeight);
        window.GetProperty("maximized").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Save_replacesAnExistingFileAndLeavesNoTempFile()
    {
        var store = CreateStore();
        store.Save(new AppSettings { PaletteHotkey = "First" });
        store.Save(new AppSettings { PaletteHotkey = "Second" });

        store.Load().PaletteHotkey.Should().Be("Second");
        Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Save_createsTheDirectoryWhenMissing()
    {
        CreateStore().Save(new AppSettings { PaletteHotkey = "Ctrl+Alt+J" });

        File.Exists(SettingsPath).Should().BeTrue();
    }

    [Fact]
    public void Save_writesTheDocumentedFileFormat()
    {
        CreateStore().Save(new AppSettings());

        var json = JsonDocument.Parse(File.ReadAllText(SettingsPath)).RootElement;
        json.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        json.GetProperty("paletteHotkey").GetString().Should().Be("Ctrl+Alt+Space");
        json.GetProperty("snippetsEnabled").GetBoolean().Should().BeTrue();
        json.GetProperty("startWithWindows").GetBoolean().Should().BeTrue();
        json.GetProperty("aiEnabled").GetBoolean().Should().BeFalse();
        json.GetProperty("activeModelId").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("unloadAfterIdleMinutes").GetInt32().Should().Be(10);
        json.GetProperty("checkForBetterModelsWeekly").GetBoolean().Should().BeFalse();
        json.GetProperty("checkForAppUpdatesWeekly").GetBoolean().Should().BeFalse();
        json.GetProperty("lastCatalogCheckUtc").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("excludedApps").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe");
        json.GetProperty("theme").GetString().Should().Be("system");
        json.GetProperty("window").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Load_withCorruptFile_renamesItAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not settings JSON");

        var settings = CreateStore().Load();

        settings.Should().BeEquivalentTo(new AppSettings());
        File.Exists(SettingsPath).Should().BeFalse();
        File.Exists(CorruptPath).Should().BeTrue();
        File.ReadAllText(CorruptPath).Should().Be("{ this is not settings JSON");
    }

    [Fact]
    public void Load_withCorruptFile_overwritesAnOldCorruptFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ corrupt now");
        File.WriteAllText(CorruptPath, "{ corrupt before");

        _ = CreateStore().Load();

        File.ReadAllText(CorruptPath).Should().Be("{ corrupt now");
    }

    [Fact]
    public void Load_afterCorruptFile_canStillSave()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "not json");

        _ = store.Load();
        store.Save(new AppSettings { AiEnabled = true });

        store.Load().AiEnabled.Should().BeTrue();
    }
}