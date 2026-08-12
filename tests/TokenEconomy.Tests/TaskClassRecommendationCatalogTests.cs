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
        Assert.Equal("task-class-taxonomy-v1", catalog.TaxonomyVersion);
        Assert.All(catalog.Recommendations,
            item => Assert.Equal(catalog.RationaleVersion, item.RationaleVersion));

        var html = catalog.Recommend(TaskClass.HtmlUiImplementation);
        Assert.Equal("gpt-5.6-sol", html.Recommended.Model.Value);
        Assert.Equal(EffortLevel.Medium, html.Recommended.ThinkingLevel);
        Assert.Equal("terra-medium", html.Downgrade?.RouteId);

        var security = catalog.Recommend(TaskClass.SecurityAssessment);
        Assert.Equal("sol-xhigh", security.Recommended.RouteId);
        Assert.Null(security.Downgrade);
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
}
