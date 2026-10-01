using FluentAssertions;
using Wordwright.Core.Models;

namespace Wordwright.Core.Tests;

public class CatalogParserTests
{
    private const string ValidCatalog = """
        {
          "schemaVersion": 1,
          "catalogVersion": "2026-10-01",
          "models": [
            {
              "id": "tiny-q4km",
              "displayName": "Tiny",
              "tiers": ["minimal", "cpu8"],
              "ramRequiredGB": 1.5,
              "sizeBytes": 500000000,
              "sha256": "aa",
              "speed": { "cpu8": { "promptTps": [60, 150], "genTps": [15, 30], "provisional": true } },
              "evalScores": { "overall": 3.4, "fix": 3.6 },
              "status": "approved"
            }
          ]
        }
        """;

    [Fact]
    public void AGoodCatalog_loadsWithItsEntries()
    {
        var catalog = CatalogParser.Parse(ValidCatalog);

        catalog.Should().NotBeNull();
        catalog!.SchemaVersion.Should().Be(1);
        catalog.CatalogVersion.Should().Be("2026-10-01");
        catalog.Models.Should().HaveCount(1);

        var model = catalog.Models[0];
        model.Id.Should().Be("tiny-q4km");
        model.Tiers.Should().Equal("minimal", "cpu8");
        model.IsApproved.Should().BeTrue();
        model.EvalScores!.Overall.Should().Be(3.4);
        model.SpeedFor("cpu8")!.SlowestGen.Should().Be(15);
    }

    [Fact]
    public void FieldsWeDoNotKnow_areIgnored()
    {
        var json = ValidCatalog.Replace(
            "\"catalogVersion\": \"2026-10-01\",",
            "\"catalogVersion\": \"2026-10-01\", \"somethingNew\": { \"nested\": [1, 2] },");

        CatalogParser.Parse(json)!.Models.Should().HaveCount(1);
    }

    [Fact]
    public void ScoresWithoutAnOverall_readAsZero()
    {
        // docs/EVAL.md also writes a score per action; only overall is read here.
        var json = ValidCatalog.Replace(
            "\"evalScores\": { \"overall\": 3.4, \"fix\": 3.6 }",
            "\"evalScores\": { \"fix\": 3.6, \"shorten\": 3.1 }");

        CatalogParser.Parse(json)!.Models[0].EvalScores!.Overall.Should().Be(0);
    }

    [Fact]
    public void ANewerSchema_isRefusedWhole()
    {
        var json = ValidCatalog.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2");

        CatalogParser.Parse(json).Should().BeNull("a newer schema may mean fields we cannot read");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("{ this is not JSON")]
    [InlineData("[]")]
    [InlineData("""{ "schemaVersion": 1, "models": [] }""")]
    public void AnUnusableCatalog_isNull(string? json)
    {
        CatalogParser.Parse(json).Should().BeNull();
    }

    [Fact]
    public void EntriesThatCannotBeUsed_areDropped()
    {
        var json = """
            {
              "schemaVersion": 1,
              "catalogVersion": "2026-10-01",
              "models": [
                { "id": "", "tiers": ["cpu8"] },
                { "id": "no-tiers", "tiers": [] },
                { "id": "keeper", "tiers": ["cpu8"] }
              ]
            }
            """;

        var catalog = CatalogParser.Parse(json)!;

        catalog.Models.Should().HaveCount(1);
        catalog.Models[0].Id.Should().Be("keeper");
    }

    [Fact]
    public void TheEmbeddedCatalog_carriesProvisionalLoadTimes()
    {
        // The consent dialogue's "first rewrite takes about N seconds longer"
        // needs one per tier; docs/MODELS.md says calibration replaces them.
        var catalog = CatalogParser.Embedded();

        foreach (var model in catalog.Models)
        {
            foreach (var (tier, speed) in model.Speed)
            {
                speed.LoadSeconds.Should().BeGreaterThan(0, $"{model.Id} on {tier} needs a load time");
                speed.Provisional.Should().BeTrue($"{model.Id} on {tier} has not been measured yet");
            }
        }
    }

    [Fact]
    public void TheEmbeddedCatalog_isAlwaysUsable()
    {
        var catalog = CatalogParser.Embedded();

        catalog.SchemaVersion.Should().Be(CatalogParser.SupportedSchemaVersion);
        catalog.CatalogVersion.Should().NotBeEmpty();
        catalog.Models.Should().NotBeEmpty("the app has to be able to offer something");
        catalog.Models.Should().OnlyContain(model => model.Id.Length > 0 && model.Tiers.Count > 0);
    }
}
