using FluentAssertions;
using Wordwright.Core.Actions;

namespace Wordwright.Core.Tests;

public class ActionStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "wordwright-actions-" + Guid.NewGuid().ToString("N"));

    public ActionStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Load_beforeAnyFile_givesTheBuiltInActions()
    {
        var store = new ActionStore(_directory);

        var document = store.Load();

        document.Actions.Should().HaveCount(6);
        document.Actions[0].Id.Should().Be("fix");
        document.Actions[0].Hotkey.Should().Be("Ctrl+Alt+G");
        document.Actions.Should().OnlyContain(action => action.BuiltIn);
    }

    [Fact]
    public void LoadOrSeed_writesTheFileOnceAndDoesNotOverwriteIt()
    {
        var store = new ActionStore(_directory);

        var seeded = store.LoadOrSeed();
        var edited = seeded with { Actions = [seeded.Actions[1]] };
        store.Save(edited);

        var again = store.LoadOrSeed();

        again.Actions.Should().HaveCount(1);
        again.Actions[0].Id.Should().Be("clear");
    }

    [Fact]
    public void SaveAndLoad_roundTripAnAction()
    {
        var store = new ActionStore(_directory);
        var custom = new AiAction
        {
            Id = "mine",
            Name = "Mine",
            ShortcutKey = "M",
            Instruction = "Do the thing.",
            Hotkey = "Ctrl+Alt+M",
        };

        store.Save(new ActionDocument { Actions = [custom] });
        var loaded = store.Load();

        loaded.Actions.Should().ContainSingle().Which.Should().Be(custom);
    }

    [Fact]
    public void Update_replacesOnlyTheMatchingAction()
    {
        var store = new ActionStore(_directory);
        var document = ActionSeeds.Default();

        var changed = document.Actions[2] with { Name = "Renamed" };
        var updated = store.Update(document, changed);

        updated.Actions[2].Name.Should().Be("Renamed");
        updated.Actions[0].Name.Should().Be("Fix grammar and spelling");
    }

    [Fact]
    public void Reset_restoresABuiltInEditedByTheUser()
    {
        var store = new ActionStore(_directory);
        var edited = new ActionDocument
        {
            Actions = [ActionSeeds.Default().Actions[0] with { Instruction = "something else", Hotkey = null }],
        };

        var reset = store.Reset(edited, "fix");

        reset.Actions[0].Instruction.Should().Be("Correct grammar, spelling and punctuation. "
            + "Keep the meaning, tone and language. Change as little as possible.");
        reset.Actions[0].Hotkey.Should().Be("Ctrl+Alt+G");
    }

    [Fact]
    public void Reset_ignoresAnUnknownId()
    {
        var store = new ActionStore(_directory);
        var document = ActionSeeds.Default();

        store.Reset(document, "nope").Should().Be(document);
    }

    [Fact]
    public void Load_aCorruptFile_fallsBackToTheBuiltInsAndKeepsTheBadFile()
    {
        var store = new ActionStore(_directory);
        File.WriteAllText(Path.Combine(_directory, "actions.json"), "{ not json");

        var loaded = store.Load();

        loaded.Actions.Should().HaveCount(6);
        File.Exists(Path.Combine(_directory, "actions.json.corrupt")).Should().BeTrue();
    }
}
