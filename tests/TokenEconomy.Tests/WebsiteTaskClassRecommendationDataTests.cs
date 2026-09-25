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
            var publishedCandidates = new[] { row.GetProperty("recommended") }
                .Concat(row.TryGetProperty("equivalentRoutes", out var equivalents)
                    ? equivalents.EnumerateArray().ToArray() : [])
                .ToArray();
            Assert.Equal(recommendation.Candidates.Count, publishedCandidates.Length);
            for (var index = 0; index < publishedCandidates.Length; index++)
            {
                Assert.Equal(recommendation.Candidates[index].Model.Value,
                    publishedCandidates[index].GetProperty("model").GetString());
                Assert.Equal(Thinking(recommendation.Candidates[index].ThinkingLevel),
                    publishedCandidates[index].GetProperty("thinkingLevel").GetString());
            }
            Assert.Equal(recommendation.EvidenceVersion, row.GetProperty("evidenceVersion").GetString());
            foreach (var (property, routes) in new[]
            {
                ("gpt6Candidates", recommendation.Gpt6Candidates),
                ("legacyFallbacks", recommendation.LegacyFallbacks),
            })
            {
                var alternatives = row.GetProperty(property).EnumerateArray().ToArray();
                Assert.Equal(routes.Count, alternatives.Length);
                for (var index = 0; index < alternatives.Length; index++)
                {
                    var alternative = alternatives[index];
                    Assert.Equal(routes[index].Model.Value, alternative.GetProperty("model").GetString());
                    var price = ModelPriceCatalog.Default.ResolvePrice(routes[index].Model,
                        DateTime.SpecifyKind(catalog.EvidenceAsOfDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)).Price!;
                    Assert.Equal(price.InputPerMTok, alternative.GetProperty("price").GetProperty("inputPerMTok").GetDecimal());
                    Assert.Equal(price.CacheReadPerMTok, alternative.GetProperty("price").GetProperty("cacheReadPerMTok").GetDecimal());
                    Assert.Equal(price.OutputPerMTok, alternative.GetProperty("price").GetProperty("outputPerMTok").GetDecimal());
                }
            }
        }
    }

    [Fact]
    public void Historical_measurements_keep_their_original_model_attribution()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "website", "data", "task-class-recommendations.json")));
        var feature = Assert.Single(document.RootElement.GetProperty("recommendations").EnumerateArray(),
            item => item.GetProperty("taskClass").GetString() == "feature");
        Assert.Equal("gpt-6-sol", feature.GetProperty("recommended").GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Null, feature.GetProperty("outcomeRate").ValueKind);
        var historical = feature.GetProperty("historicalBaseline");
        Assert.Equal("gpt-5.6-sol", historical.GetProperty("recommended").GetProperty("model").GetString());
        Assert.Equal(0.75m, historical.GetProperty("outcomeRate").GetDecimal());
        Assert.Equal(36, historical.GetProperty("attemptCount").GetInt32());
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
