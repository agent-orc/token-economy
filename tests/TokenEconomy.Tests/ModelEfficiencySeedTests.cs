using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelEfficiencySeedTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ModelEfficiencyMatrix Matrix = ModelEfficiencyMatrix.Default;

    [Fact]
    public void EveryCatalogModel_HasExactlyOneProfile()
    {
        // The matrix must cover every priced/known model — and no more.
        Assert.Equal(ModelPriceCatalog.Default.Listings.Count, Matrix.Profiles.Count);
        foreach (var listing in ModelPriceCatalog.Default.Listings)
            Assert.NotNull(Matrix.Find(listing.ModelId));
    }

    [Fact]
    public void EveryProfile_ResolvesInTheCatalogAndHasEffortLevels()
    {
        foreach (var profile in Matrix.Profiles)
        {
            Assert.NotNull(ModelPriceCatalog.Default.Find(profile.ModelId));
            Assert.NotEmpty(profile.EffortLevels);
        }
    }

    [Fact]
    public void OpusFamily_IsFrontier_AndPremiumCost()
    {
        foreach (var id in new[] { "claude-opus-4-8", "claude-opus-4-7", "claude-opus-4-6", "claude-opus-4-5" })
        {
            Assert.Equal(CapabilityTier.Frontier, Matrix.Find(id)!.Tier);
            Assert.Equal(CostClass.Premium, Matrix.CostClassOf(id, Now));
        }
    }

    [Fact]
    public void SonnetFamily_IsBalanced_AndStandardCost()
    {
        foreach (var id in new[] { "claude-sonnet-5", "claude-sonnet-4-6", "claude-sonnet-4-5" })
        {
            Assert.Equal(CapabilityTier.Balanced, Matrix.Find(id)!.Tier);
            Assert.Equal(CostClass.Standard, Matrix.CostClassOf(id, Now));
        }
    }

    [Fact]
    public void Haiku_IsLight_EconomyCost_AndCapsAtMediumEffort()
    {
        var haiku = Matrix.Find("claude-haiku-4-5")!;
        Assert.Equal(CapabilityTier.Light, haiku.Tier);
        Assert.Equal(CostClass.Economy, Matrix.CostClassOf("claude-haiku-4-5", Now));
        Assert.DoesNotContain(EffortLevel.High, haiku.EffortLevels);
    }

    [Fact]
    public void GptFamily_RunsOnCodex_AndIsUnpricedHenceUnknownCost()
    {
        foreach (var id in new[] { "gpt-5.6", "gpt-5.5", "gpt-5", "gpt-5-codex" })
        {
            Assert.Equal(Cli.Codex, Matrix.CliOf(id));
            Assert.Equal(CostClass.Unknown, Matrix.CostClassOf(id, Now));
        }
    }

    [Fact]
    public void Mythos_IsRestricted_AndOpus41_IsDeprecated()
    {
        Assert.True(Matrix.Find("claude-mythos-5")!.Restricted);
        Assert.True(Matrix.Find("claude-opus-4-1")!.Deprecated);
    }
}
