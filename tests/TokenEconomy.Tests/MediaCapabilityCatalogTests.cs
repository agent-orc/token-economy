using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class MediaCapabilityCatalogTests
{
    private static readonly DateTime Observed = new(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Default_HasCompleteEightCapabilityMatrixForEveryCli()
    {
        var catalog = MediaCapabilityCatalog.Default;

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(new DateOnly(2026, 7, 24), catalog.AsOfDate);
        foreach (var cli in new[] { "codex", "antigravity", "claude-code" })
        {
            var rows = catalog.Pull(cli, "any-selected-model");
            Assert.Equal(Enum.GetValues<MediaCapability>().Length, rows.Count);
            Assert.Equal(Enum.GetValues<MediaCapability>().Order(), rows.Select(row => row.Capability).Order());
            Assert.All(rows, row =>
            {
                Assert.NotEmpty(row.InvocationPath);
                Assert.NotEmpty(row.Evidence);
                Assert.All(row.Evidence, evidence => Assert.Equal(DateTimeKind.Utc, evidence.ObservedAtUtc.Kind));
            });
        }
    }

    [Fact]
    public void Find_UsesCliHostWildcardForAnySelectedModel()
    {
        var row = MediaCapabilityCatalog.Default.Find("CODEX", "gpt-5.6-sol", MediaCapability.ImageGeneration);

        Assert.NotNull(row);
        Assert.Equal("*", row!.ModelId);
        Assert.True(row.Supported);
        Assert.Equal(MediaCostFactorStatus.UnverifiedClaim, row.CostFactor.Status);
        Assert.Equal(3m, row.CostFactor.Minimum);
        Assert.Equal(5m, row.CostFactor.Maximum);
    }

    [Fact]
    public void ExactModelRecord_WinsOverCliHostWildcard()
    {
        var catalog = new MediaCapabilityCatalog([
            Row("*", supported: false),
            Row("model-a", supported: true),
        ]);

        Assert.True(catalog.Find("test-cli", "model-a", MediaCapability.ImageGeneration)!.Supported);
        Assert.False(catalog.Find("test-cli", "model-b", MediaCapability.ImageGeneration)!.Supported);
    }

    [Fact]
    public void VerifiedResearchCorrections_AreRepresented()
    {
        var catalog = MediaCapabilityCatalog.Default;

        Assert.True(catalog.Find("claude-code", "claude-opus-4-8", MediaCapability.VoiceDictation)!.Supported);
        Assert.False(catalog.Find("claude-code", "claude-opus-4-8", MediaCapability.ImageGeneration)!.Supported);
        Assert.True(catalog.Find("claude-code", "claude-opus-4-8", MediaCapability.ImageUnderstanding)!.Supported);
        Assert.True(catalog.Find("antigravity", "gemini-3.5-flash", MediaCapability.ImageGeneration)!.Supported);
        Assert.False(catalog.Find("antigravity", "gemini-3.5-flash", MediaCapability.Video)!.Supported);
    }

    [Fact]
    public void UnknownCliAndBlankInputs_ReturnNoRows()
    {
        var catalog = MediaCapabilityCatalog.Default;

        Assert.Null(catalog.Find("unknown", "model", MediaCapability.ImageGeneration));
        Assert.Null(catalog.Find("", "model", MediaCapability.ImageGeneration));
        Assert.Empty(catalog.Pull("unknown", "model"));
    }

    [Fact]
    public void Constructor_RejectsDuplicatesAfterNormalization()
    {
        Assert.Throws<ArgumentException>(() => new MediaCapabilityCatalog([
            Row("*", supported: true),
            Row(" * ", supported: false),
        ]));
    }

    [Fact]
    public void Constructor_RejectsClaimedFactorWithoutComparableRange()
    {
        var invalid = Row("*", supported: true) with
        {
            CostFactor = new() { Status = MediaCostFactorStatus.UnverifiedClaim, RelativeTo = "normal turn" },
        };

        Assert.Throws<ArgumentException>(() => new MediaCapabilityCatalog([invalid]));
    }

    private static MediaCapabilityRecord Row(string model, bool supported) => new()
    {
        CliId = "test-cli",
        ModelId = model,
        Capability = MediaCapability.ImageGeneration,
        Supported = supported,
        InvocationPath = supported ? "natural-language prompt" : "not available natively",
        CostFactor = new()
        {
            Status = supported ? MediaCostFactorStatus.Unknown : MediaCostFactorStatus.NotApplicable,
            Note = "test row",
        },
        Evidence =
        [
            new()
            {
                Source = MediaEvidenceSource.ControlledBenchmark,
                ObservedAtUtc = Observed,
                Reference = "benchmarks/results/test.json",
                Note = "test evidence",
            },
        ],
    };
}
