using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class Gpt61SolSupportTests
{
    private static readonly DateTime At = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReleaseDateGatesTheProviderVerifiedStandardPrice()
    {
        var catalog = ModelPriceCatalog.Default;
        var listing = catalog.Find(KnownModels.Gpt61Sol)!;
        var price = catalog.ResolvePrice(KnownModels.Gpt61Sol, At).Price!;

        Assert.Equal(new DateOnly(2026, 9, 29), listing.ReleaseDate);
        Assert.Equal((2m, .10m, 2.50m, 10m),
            (price.InputPerMTok, price.CacheReadPerMTok, price.CacheWritePerMTok, price.OutputPerMTok));
        Assert.False(price.Unconfirmed);
        Assert.Equal(PriceStatus.NoPriceForDate, catalog.ResolvePrice(KnownModels.Gpt61Sol,
            new DateTime(2026, 9, 28, 23, 59, 59, DateTimeKind.Utc)).Status);
        Assert.Equal(KnownModels.Gpt56Sol.Value, catalog.Find("sol")!.ModelId);
    }

    [Theory]
    [InlineData(EffortLevel.Minimal, "minimal")]
    [InlineData(EffortLevel.Low, "low")]
    [InlineData(EffortLevel.Medium, "medium")]
    [InlineData(EffortLevel.High, "high")]
    [InlineData(EffortLevel.XHigh, "xhigh")]
    public void ExplicitPinAndTypedEvaluationUseTheDiscoveredLadder(EffortLevel effort, string level)
    {
        var suggestion = ModelEfficiencyMatrix.Default.EvaluateModel(
            KnownModels.Gpt61Sol, TaskClass.Feature, BudgetPressure.Tight, At, effort)!;
        var resolution = ModelRoutingKnowledgeBase.Default.Resolve("gpt-6-1-sol", level,
            RoutingWorkflowRole.CoreTask);

        Assert.Equal(effort, suggestion.SuggestedEffort);
        Assert.Equal(Cli.Codex, suggestion.Cli);
        Assert.True(suggestion.Provisional);
        Assert.Equal(PolicyEvidenceStatus.Provisional, suggestion.EvidenceStatus);
        Assert.True(resolution.IsResolved, resolution.Reason);
        Assert.Equal(KnownModels.Gpt61Sol.Value, resolution.Model!.CanonicalId);
        Assert.True(resolution.Model.Provisional);
    }

    [Theory]
    [InlineData("max")]
    [InlineData("ultra")]
    public void SourceOnlyLevelsAreRejected(string level)
        => Assert.Equal(ModelRouteResolutionStatus.UnsupportedThinkingLevel,
            ModelRoutingKnowledgeBase.Default.Resolve(KnownModels.Gpt61Sol.Value, level,
                RoutingWorkflowRole.CoreTask).Status);

    [Fact]
    public void NewModelHasNoAutomaticRoutePriorOrFallback()
    {
        var id = KnownModels.Gpt61Sol.Value;
        var knowledge = ModelRoutingKnowledgeBase.Default;
        Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == id);
        Assert.DoesNotContain(knowledge.ProviderFallbacks, route => route.ModelId == id);
        Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
            recommendation => Assert.DoesNotContain(recommendation.Candidates,
                candidate => candidate.Model == KnownModels.Gpt61Sol));
    }

    [Fact]
    public void SuccessorMigrationIsProposalOnlyWithExplicitDowngrade()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src/TokenEconomy/catalog/model-migrations.v1.json")));
        var migration = Assert.Single(document.RootElement.GetProperty("migrations").EnumerateArray()
            .Where(item => item.GetProperty("from").GetString() == KnownModels.Gpt6Sol.Value
                && item.GetProperty("to").GetString() == KnownModels.Gpt61Sol.Value));

        Assert.False(migration.GetProperty("ladderCompatible").GetBoolean());
        Assert.False(migration.GetProperty("safeAuto").GetBoolean());
        Assert.Equal("none", migration.GetProperty("evidence").GetProperty("kind").GetString());
        Assert.Contains("max and ultra", migration.GetProperty("note").GetString());
        Assert.Contains("xhigh", migration.GetProperty("note").GetString());
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "TokenEconomy.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
