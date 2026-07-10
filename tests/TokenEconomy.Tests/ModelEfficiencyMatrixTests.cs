using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelEfficiencyMatrixTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ModelEfficiencyMatrix Matrix = ModelEfficiencyMatrix.Default;

    // ---- construction / validation ----

    [Fact]
    public void Default_BuildsOverTheDefaultCatalog()
    {
        Assert.NotEmpty(Matrix.Profiles);
        Assert.Same(ModelPriceCatalog.Default, Matrix.Catalog);
    }

    [Fact]
    public void Constructor_RejectsNullCatalog()
        => Assert.Throws<ArgumentNullException>(() => new ModelEfficiencyMatrix(null!, []));

    [Fact]
    public void Constructor_RejectsProfileForModelNotInCatalog()
        => Assert.Throws<ArgumentException>(() => new ModelEfficiencyMatrix(
            ModelPriceCatalog.Default,
            [new ModelEfficiencyProfile { ModelId = "totally-made-up", Tier = CapabilityTier.Light }]));

    [Fact]
    public void Constructor_RejectsProfileWithNoEffortLevels()
        => Assert.Throws<ArgumentException>(() => new ModelEfficiencyMatrix(
            ModelPriceCatalog.Default,
            [new ModelEfficiencyProfile { ModelId = "claude-opus-4-8", Tier = CapabilityTier.Frontier, EffortLevels = [] }]));

    [Fact]
    public void Constructor_RejectsTwoProfilesForTheSameModel()
        => Assert.Throws<ArgumentException>(() => new ModelEfficiencyMatrix(
            ModelPriceCatalog.Default,
            [
                new ModelEfficiencyProfile { ModelId = "claude-haiku-4-5", Tier = CapabilityTier.Light },
                new ModelEfficiencyProfile { ModelId = "claude-haiku-4-5-20251001", Tier = CapabilityTier.Light }, // alias of the same model
            ]));

    // ---- lookups ----

    [Theory]
    [InlineData("claude-opus-4-8")]
    [InlineData("Claude-Opus-4.8")]                 // case + dot/dash folded via the catalog
    [InlineData("claude-haiku-4-5-20251001")]       // alias resolves to its listing
    public void Find_ResolvesThroughTheCatalog(string id)
        => Assert.NotNull(Matrix.Find(id));

    [Fact]
    public void Find_ReturnsNullForUnknownModel()
        => Assert.Null(Matrix.Find("no-such-model"));

    [Theory]
    [InlineData("anthropic", Cli.Claude)]
    [InlineData("ANTHROPIC", Cli.Claude)]
    [InlineData("openai", Cli.Codex)]
    public void CliForVendor_MapsKnownVendors(string vendor, Cli expected)
        => Assert.Equal(expected, ModelEfficiencyMatrix.CliForVendor(vendor));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("google")]
    public void CliForVendor_IsNullForUnknownVendor(string? vendor)
        => Assert.Null(ModelEfficiencyMatrix.CliForVendor(vendor));

    [Fact]
    public void CliOf_DerivesFromCatalogVendor()
    {
        Assert.Equal(Cli.Claude, Matrix.CliOf("claude-opus-4-8"));
        Assert.Equal(Cli.Codex, Matrix.CliOf("gpt-5.6"));
        Assert.Null(Matrix.CliOf("no-such-model"));
    }

    // ---- derived cost class ----

    [Theory]
    [InlineData("claude-opus-4-8", CostClass.Premium)]
    [InlineData("claude-fable-5", CostClass.Premium)]
    [InlineData("claude-sonnet-4-6", CostClass.Standard)]
    [InlineData("claude-sonnet-5", CostClass.Standard)]   // introductory rate now, still Standard
    [InlineData("claude-haiku-4-5", CostClass.Economy)]
    [InlineData("gpt-5.6", CostClass.Unknown)]            // known model, unpriced → Unknown, never guessed
    public void CostClassOf_IsDerivedFromThePricingCatalog(string model, CostClass expected)
        => Assert.Equal(expected, Matrix.CostClassOf(model, Now));

    [Fact]
    public void SuitabilityOf_UsesTheProfileTier()
    {
        Assert.Equal(Suitability.Ideal, Matrix.SuitabilityOf("claude-opus-4-8", TaskClass.HeavyDesign));
        Assert.Equal(Suitability.Ideal, Matrix.SuitabilityOf("claude-haiku-4-5", TaskClass.MechanicalChore));
        Assert.Null(Matrix.SuitabilityOf("no-such-model", TaskClass.Feature));
    }

    // ---- Describe: the matrix as inspectable data ----

    [Fact]
    public void Describe_HasOneRowPerCatalogModel()
    {
        var rows = Matrix.Describe(Now);
        Assert.Equal(ModelPriceCatalog.Default.Listings.Count, rows.Count);

        // Every catalog model has a profile (task item 1: "per model ... from the pricing catalog").
        foreach (var listing in ModelPriceCatalog.Default.Listings)
            Assert.NotNull(Matrix.Find(listing.ModelId));
    }

    [Fact]
    public void Describe_FillsEveryTaskClassCell()
    {
        foreach (var row in Matrix.Describe(Now))
            foreach (var task in Enum.GetValues<TaskClass>())
                Assert.True(row.Suitability.ContainsKey(task), $"{row.ModelId} missing {task}");
    }

    [Fact]
    public void Describe_CarriesTierCostEffortAndFlags()
    {
        var rows = Matrix.Describe(Now);

        var opus = rows.Single(r => r.ModelId == "claude-opus-4-8");
        Assert.Equal(CapabilityTier.Frontier, opus.Tier);
        Assert.Equal(CostClass.Premium, opus.CostClass);
        Assert.Equal(Cli.Claude, opus.Cli);
        Assert.Equal(Suitability.Ideal, opus.Suitability[TaskClass.HeavyDesign]);
        Assert.Equal(Suitability.Overkill, opus.Suitability[TaskClass.MechanicalChore]);

        var haiku = rows.Single(r => r.ModelId == "claude-haiku-4-5");
        Assert.Equal(CostClass.Economy, haiku.CostClass);
        Assert.Equal([EffortLevel.Low, EffortLevel.Medium], haiku.EffortLevels);

        var gpt = rows.Single(r => r.ModelId == "gpt-5.6");
        Assert.Equal(CostClass.Unknown, gpt.CostClass);
        Assert.Equal(Cli.Codex, gpt.Cli);
    }

    [Fact]
    public void Describe_FlagsRestrictedDeprecatedAndUnconfirmed()
    {
        var rows = Matrix.Describe(Now);
        Assert.True(rows.Single(r => r.ModelId == "claude-mythos-5").Restricted);
        Assert.True(rows.Single(r => r.ModelId == "claude-opus-4-1").Deprecated);
        Assert.True(rows.Single(r => r.ModelId == "claude-opus-4-5").CostUnconfirmed);
    }
}
