using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class WebsiteLanguageCapabilityDataTests
{
    [Fact]
    public void Website_keeps_catalog_scores_costs_status_and_provenance_without_filling_unknowns()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(LanguageCapabilityCatalogTests.Root(),
            "website/data/language-capabilities.json")));
        var root = json.RootElement;
        var rows = root.GetProperty("records").EnumerateArray().ToArray();
        Assert.Equal(LanguageCapabilityCatalog.Default.Records.Count, rows.Length);
        Assert.Equal(LanguageCapabilityCatalog.Default.Studies.Count, root.GetProperty("studies").GetArrayLength());
        foreach (var record in LanguageCapabilityCatalog.Default.Records)
        {
            var published = Assert.Single(rows, r => r.GetProperty("id").GetString() == record.Id);
            Assert.Equal(record.ModelId, published.GetProperty("modelId").GetString());
            Assert.Equal(record.Language, published.GetProperty("language").GetString());
            Assert.Equal(record.Status.ToString().ToLowerInvariant(), published.GetProperty("status").GetString());
            Assert.Equal(record.Overall, NullableNumber(published, "overall"));
            Assert.Equal(record.CostPerSampleUsd, NullableNumber(published, "costPerSampleUsd"));
            Assert.Equal(record.Evidence.Count, published.GetProperty("evidence").GetArrayLength());
            var quality = Assert.Single(root.GetProperty("costQuality").EnumerateArray(), r => r.GetProperty("recordId").GetString() == record.Id);
            Assert.Null(NullableNumber(quality, "costPerQualityPointUsd"));
        }
        Assert.Equal(TaskClassRecommendationCatalog.Default.LanguagePriors.Count, root.GetProperty("languagePriors").GetArrayLength());
    }

    private static decimal? NullableNumber(JsonElement value, string name) =>
        value.GetProperty(name).ValueKind == JsonValueKind.Null ? null : value.GetProperty(name).GetDecimal();
}
