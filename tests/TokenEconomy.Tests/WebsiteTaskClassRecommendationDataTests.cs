using System.Text.Json;
using System.Text.Json.Serialization;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class WebsiteTaskClassRecommendationDataTests
{
    [Fact]
    public void Website_projection_matches_public_recommendation_api()
    {
        var root = RepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "website", "data", "task-class-recommendations.json")));
        var published = document.RootElement;
        var catalog = TaskClassRecommendationCatalog.Default;

        Assert.Equal(catalog.TaxonomyVersion, published.GetProperty("taxonomyVersion").GetString());
        Assert.Equal(catalog.RationaleVersion, published.GetProperty("rationaleVersion").GetString());
        var rows = published.GetProperty("recommendations").EnumerateArray().ToArray();
        Assert.Equal(catalog.Recommendations.Count, rows.Length);
        foreach (var recommendation in catalog.Recommendations)
        {
            var row = Assert.Single(rows, row => row.GetProperty("id").GetString() == recommendation.Id);
            Assert.Equal(recommendation.Recommended.Model.Value,
                row.GetProperty("recommended").GetProperty("model").GetString());
            Assert.Equal(Thinking(recommendation.Recommended.ThinkingLevel),
                row.GetProperty("recommended").GetProperty("thinkingLevel").GetString());
            Assert.Equal(recommendation.EvidenceVersion, row.GetProperty("evidenceVersion").GetString());
        }
    }

    [Fact]
    public void Recommendation_schemas_and_study_documents_are_well_formed()
    {
        var root = RepositoryRoot();
        using var recommendationSchema = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "TokenEconomy", "catalog", "task-class-recommendations.schema.json")));
        Assert.Equal(1, recommendationSchema.RootElement.GetProperty("properties")
            .GetProperty("schemaVersion").GetProperty("const").GetInt32());
        foreach (var name in new[] { "html-ui-v1.json", "source-code-review-v1.json" })
        {
            using var study = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(root, "benchmarks", "task-class-studies", name)));
            Assert.Equal(1, study.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("pilotComplete", study.RootElement.GetProperty("status").GetString());
            Assert.True(study.RootElement.GetProperty("scenarios").GetArrayLength() >= 2);
        }
    }

    private static string Thinking(EffortLevel value) => value switch
    {
        EffortLevel.XHigh => "xHigh",
        _ => value.ToString().ToLowerInvariant(),
    };

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
