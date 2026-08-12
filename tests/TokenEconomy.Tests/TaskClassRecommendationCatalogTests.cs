using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class TaskClassRecommendationCatalogTests
{
    [Fact]
    public void Catalog_covers_taxonomy_and_exposes_versioned_card_creation_route()
    {
        var catalog = TaskClassRecommendationCatalog.Default;

        Assert.Equal(Enum.GetValues<TaskClass>().Length, catalog.Recommendations.Count);
        Assert.Equal("task-class-taxonomy-v2", catalog.TaxonomyVersion);
        Assert.All(catalog.Recommendations,
            item => Assert.Equal(catalog.RationaleVersion, item.RationaleVersion));

        var html = catalog.Recommend(TaskClass.HtmlUiImplementation);
        Assert.Equal("gpt-5.6-sol", html.Candidates[0].Model.Value);
        Assert.Equal(EffortLevel.Medium, html.Candidates[0].ThinkingLevel);
        Assert.Equal(2, html.Candidates.Count);
        Assert.Equal(TaskClassEquivalenceStatus.Provisional, html.Equivalence);
        Assert.Equal("terra-medium", html.Downgrade?.RouteId);

        var security = catalog.Recommend(TaskClass.SecurityAssessment);
        Assert.Equal("sol-xhigh", security.Recommended.RouteId);
        Assert.Null(security.Downgrade);
    }

    [Fact]
    public void Planning_and_decision_making_are_strong_ideal_sets()
    {
        var catalog = TaskClassRecommendationCatalog.Default;

        foreach (var taskClass in new[] { TaskClass.Planning, TaskClass.DecisionMaking })
        {
            var recommendation = catalog.Recommend(taskClass);
            Assert.Equal(CapabilityTier.Frontier, recommendation.MinimumCapability);
            Assert.Equal(2, recommendation.Candidates.Count);
            Assert.All(recommendation.Candidates, candidate =>
                Assert.Equal(CapabilityTier.Frontier,
                    ModelRoutingKnowledgeBase.Default.FindModel(candidate.Model.Value)!.CapabilityTier));
            Assert.Null(recommendation.Downgrade);
        }
    }

    [Fact]
    public void Selection_uses_fresh_quota_across_the_set_and_unknown_quota_never_selects()
    {
        var catalog = TaskClassRecommendationCatalog.Default;
        var recommendation = catalog.Recommend(TaskClass.HtmlUiImplementation);
        var at = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var fresh = new ProviderAvailabilitySnapshot(at, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15),
            [Provider("openai", "codex", at, 50), Provider("anthropic", "claude", at, 20)]);

        var selected = catalog.Select(recommendation, fresh, at);

        Assert.Equal(TaskClassSelectionDisposition.Selected, selected.Disposition);
        Assert.Equal("claude-sonnet-5", selected.Selected?.Model.Value);

        var stale = fresh with { DecisionAtUtc = at.AddHours(-1) };
        var recommendationOnly = catalog.Select(recommendation, stale, at);
        Assert.Equal(TaskClassSelectionDisposition.RecommendationOnly, recommendationOnly.Disposition);
        Assert.Null(recommendationOnly.Selected);
    }

    [Fact]
    public void Controlled_coding_recommendation_retains_outcome_and_cost_evidence()
    {
        var feature = TaskClassRecommendationCatalog.Default.Recommend(TaskClass.Feature);

        Assert.Equal(TaskClassRecommendationStatus.ControlledCodingEvidence, feature.Status);
        Assert.Equal(4, feature.ScenarioCount);
        Assert.Equal(36, feature.AttemptCount);
        Assert.Equal(0.75m, feature.OutcomeRate);
        Assert.True(feature.CostPerSuccessfulOutcome?.AmountUsd > 0);
        Assert.All(feature.Evidence, line => Assert.False(string.IsNullOrWhiteSpace(line.Reference)));
    }

    private static ProviderAvailabilitySnapshotRow Provider(
        string provider, string cli, DateTime at, long used) => new(
            provider, cli, ProviderCliAvailability.Available, null, at,
            SnapshotFreshness.Fresh, AvailabilityWarningState.Healthy,
            0, 0, new(at, SnapshotCostStatus.Priced, []),
            [new("five-hour", at.AddHours(-1), at.AddHours(4), SnapshotFreshness.Fresh,
                AvailabilityWarningState.Healthy,
                new(SnapshotValueOrigin.Observed, used, 100, 100 - used, used, at), null)], []);
}
