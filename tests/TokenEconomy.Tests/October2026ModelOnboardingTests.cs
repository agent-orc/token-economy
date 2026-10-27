using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class October2026ModelOnboardingTests
{
    private static readonly DateTime At = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OnboardedTariffsHaveCurrentPrimaryProvenanceAndNoInventedCacheWrites()
    {
        foreach (var model in new[] { KnownModels.ClaudeHaiku55, KnownModels.Gemini4Argon,
                     KnownModels.Gemini38Flash })
        {
            var listing = ModelPriceCatalog.Default.Find(model)!;
            Assert.NotNull(listing.ReleaseDate);
            Assert.StartsWith("https://", listing.ReleaseDateSource);
            foreach (var price in listing.History)
            {
                Assert.InRange(price.VerifiedOn!.Value, new DateOnly(2026, 10, 16),
                    DateOnly.FromDateTime(DateTime.UtcNow));
                Assert.Equal("provider-announced-date", price.ValidFromBasis);
                Assert.False(price.Unconfirmed);
                Assert.NotEmpty(price.SourceUrls);
                Assert.All(price.SourceUrls, url => Assert.StartsWith("https://", url));
                if (listing.Vendor == "google") Assert.Null(price.CacheWritePerMTok);
                else Assert.True(price.CacheWritePerMTok > 0);
            }
        }
    }

    [Theory]
    [InlineData(100_000, 0.10, 0.50, 0.01, 0.125)]
    [InlineData(100_001, 0.50, 2.50, 0.05, 0.625)]
    public void HaikuPromptBoundaryPricesTheWholeRequest(long promptTokens, double input, double output,
        double cacheRead, double cacheWrite)
    {
        var cost = ModelPriceCatalog.Default.ComputeCost(KnownModels.ClaudeHaiku55,
            new TokenUsage(40_000, 20_000, 40_000, 20_000, promptTokens), At);

        Assert.Equal(PriceStatus.Resolved, cost.Status);
        Assert.Equal(40_000m / 1_000_000m * (decimal)input, cost.InputCost);
        Assert.Equal(20_000m / 1_000_000m * (decimal)output, cost.OutputCost);
        Assert.Equal(40_000m / 1_000_000m * (decimal)cacheRead, cost.CacheReadCost);
        Assert.Equal(20_000m / 1_000_000m * (decimal)cacheWrite, cost.CacheWriteCost);
        Assert.Equal(cost.InputCost + cost.OutputCost + cost.CacheReadCost + cost.CacheWriteCost, cost.Total);
        Assert.Equal(promptTokens > 100_000 ? 100_001L : null, cost.AppliedPromptSizeTier?.MinPromptTokens);
    }

    [Fact]
    public void TieredCostWithoutPerRequestPromptSizeIsAmbiguous()
    {
        var cost = ModelPriceCatalog.Default.ComputeCost(KnownModels.ClaudeHaiku55,
            new TokenUsage(40_000, 20_000, 40_000, 20_000), At);
        Assert.Equal(PriceStatus.AmbiguousPromptSize, cost.Status);
        Assert.Null(cost.Total);
        Assert.False(cost.HasPrice);
    }

    [Fact]
    public void ExistingGpt61ThresholdUsesTheSameTierMechanism()
    {
        var before = ModelPriceCatalog.Default.ComputeCost(KnownModels.Gpt61Sol,
            new TokenUsage(100_000, 10_000, PromptTokens: 272_000), At);
        var after = ModelPriceCatalog.Default.ComputeCost(KnownModels.Gpt61Sol,
            new TokenUsage(100_000, 10_000, PromptTokens: 272_001), At);
        Assert.Equal(0.30m, before.Total);
        Assert.Equal(0.55m, after.Total);
    }

    [Fact]
    public void ArgonAnnouncedPriceChangeIsDatedAndNotPremature()
    {
        var catalog = ModelPriceCatalog.Default;
        var usage = new TokenUsage(1_000_000, 1_000_000, 1_000_000);
        Assert.Equal(PriceStatus.NoPriceForDate, catalog.ComputeCost(KnownModels.Gemini4Argon, usage,
            new DateTime(2026, 10, 12, 23, 59, 59, DateTimeKind.Utc)).Status);
        Assert.Equal(12.10m, catalog.ComputeCost(KnownModels.Gemini4Argon, usage,
            new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc)).Total);
        Assert.Equal(24.20m, catalog.ComputeCost(KnownModels.Gemini4Argon, usage,
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)).Total);
    }

    [Fact]
    public void FlashFutureTariffAndAgyAliasesAreVisible()
    {
        var catalog = ModelPriceCatalog.Default;
        Assert.Equal(KnownModels.Gemini38Flash.Value, catalog.Find("gemini-3.8-flash-high")?.ModelId);
        Assert.Equal(KnownModels.Gemini4Argon.Value, catalog.Find("gemini-4-argon-low")?.ModelId);
        Assert.Equal(4.575m, catalog.ComputeCost(KnownModels.Gemini38Flash,
            new TokenUsage(1_000_000, 1_000_000, 1_000_000), At).Total);
        Assert.Equal(9.15m, catalog.ComputeCost(KnownModels.Gemini38Flash,
            new TokenUsage(1_000_000, 1_000_000, 1_000_000),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Total);
    }

    [Fact]
    public void GoogleModelsRequirePinsAndHaikuCannotFillACodingRole()
    {
        var policy = ModelRoutingKnowledgeBase.Default;
        var matrix = ModelEfficiencyMatrix.Default;
        foreach (var model in new[] { KnownModels.Gemini4Argon, KnownModels.Gemini38Flash })
        {
            Assert.True(policy.FindModel(model)!.TestOnly);
            Assert.Equal(Cli.Antigravity, matrix.CliOf(model));
            Assert.True(policy.Resolve(model.Value, "medium", RoutingWorkflowRole.CoreTask).IsResolved);
            Assert.Equal(ModelRouteResolutionStatus.UnsupportedThinkingLevel,
                policy.Resolve(model.Value, "xhigh", RoutingWorkflowRole.CoreTask).Status);
            Assert.DoesNotContain(policy.Routes, route => route.ModelId == model);
            Assert.DoesNotContain(policy.ProviderFallbacks, fallback => fallback.ModelId == model);
        }
        Assert.DoesNotContain(matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Comfortable,
            [Cli.Antigravity], At), candidate => candidate.ModelId == KnownModels.Gemini4Argon
                || candidate.ModelId == KnownModels.Gemini38Flash);
        Assert.True(policy.Resolve(KnownModels.ClaudeHaiku55.Value, "low",
            RoutingWorkflowRole.BoundedPipelineDecision).IsResolved);
        Assert.Equal(ModelRouteResolutionStatus.WorkflowRoleMismatch,
            policy.Resolve(KnownModels.ClaudeHaiku55.Value, "low", RoutingWorkflowRole.CoreTask).Status);
    }

    [Fact]
    public void HaikuMigrationIsProposalOnly()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src/TokenEconomy/catalog/model-migrations.v1.json")));
        var migration = Assert.Single(document.RootElement.GetProperty("migrations").EnumerateArray()
            .Where(item => item.GetProperty("from").GetString() == KnownModels.ClaudeHaiku45.Value
                && item.GetProperty("to").GetString() == KnownModels.ClaudeHaiku55.Value));
        Assert.False(migration.GetProperty("safeAuto").GetBoolean());
        Assert.False(migration.GetProperty("ladderCompatible").GetBoolean());
        Assert.Equal("none", migration.GetProperty("evidence").GetProperty("kind").GetString());
    }

    [Fact]
    public void HaikuCliVersionGateKeepsItsBoundedRole()
    {
        var policy = ModelRoutingKnowledgeBase.Default;
        var model = policy.FindModel(KnownModels.ClaudeHaiku55.Value)!;
        Assert.Equal("2.1.293", model.MinimumCliVersion);
        Assert.Equal(new[] { "low", "medium", "high", "xhigh", "max" }, model.SupportedThinkingLevels);
        Assert.Equal(ModelRouteResolutionStatus.CliVersionTooOld,
            policy.Resolve(model.CanonicalId, "medium", RoutingWorkflowRole.BoundedPipelineDecision,
                new Version(2, 1, 284)).Status);
        Assert.True(policy.Resolve(model.CanonicalId, "medium", RoutingWorkflowRole.BoundedPipelineDecision,
            new Version(2, 1, 293)).IsResolved);
        Assert.Equal(ModelRouteResolutionStatus.WorkflowRoleMismatch,
            policy.Resolve(model.CanonicalId, "medium", RoutingWorkflowRole.CoreTask,
                new Version(2, 1, 305)).Status);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "TokenEconomy.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
