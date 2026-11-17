using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class November17CatalogueRefreshTests
{
    private static readonly DateTime AsOf = new(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("claude-fable-5-2", 8, .2, 40)]
    [InlineData("claude-mythos-5-2", 8, .2, 40)]
    [InlineData("gpt-6.2-sol", 2, .1, 10)]
    [InlineData("gpt-6.2-astra", 8, .4, 40)]
    [InlineData("gemini-4-hydrogen", 1.5, .075, 7.5)]
    public void PublishedTariffsResolveAtSnapshotWithVendorProvenance(string model, decimal input, decimal read, decimal output)
    {
        var price = ModelPriceCatalog.Default.ResolvePrice(model, AsOf).Price!;
        Assert.Equal(input, price.InputPerMTok);
        Assert.Equal(read, price.CacheReadPerMTok);
        Assert.Equal(output, price.OutputPerMTok);
        Assert.False(price.Unconfirmed);
        Assert.Contains("[V]", price.Source);
        Assert.Equal(new DateOnly(2026, 11, 17), price.VerifiedOn);
        Assert.NotEmpty(price.SourceUrls);
    }

    [Fact]
    public void NewInventoryCannotEnterDefaultRoutesOrInventUnsupportedWriteCosts()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        foreach (var model in new[] { "claude-fable-5-2", "claude-mythos-5-2", "gpt-6.2-sol",
                     "gpt-6.2-astra", "gemini-4-hydrogen", "gpt-6-cyber", "gemini-4-carbon" })
        {
            Assert.Equal(ModelRoutingStatus.Restricted, knowledge.FindModel(model)!.RoutingStatus);
            Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == model);
            Assert.DoesNotContain(knowledge.ProviderFallbacks, route => route.ModelId == model);
        }
        Assert.DoesNotContain("none", knowledge.FindModel("gpt-6.2-sol")!.SupportedThinkingLevels);
        Assert.Equal(PriceStatus.NoPriceForDate, ModelPriceCatalog.Default.ResolvePrice("gpt-6-cyber", AsOf).Status);
        Assert.Equal(PriceStatus.UnsupportedCacheRetention, ModelPriceCatalog.Default.ComputeCost(
            "gemini-4-hydrogen", new TokenUsage(0, 0, CacheWrite: 1), AsOf).Status);
    }

    [Fact]
    public void TerminalBench41RetainsRawTotalsAndDoesNotReplace40Scores()
    {
        var catalogue = BenchmarkEvidenceCatalog.Default;
        var rows = catalogue.Results.Where(row => row.Id.StartsWith("te82-tb41-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(11, rows.Length);
        foreach (var row in rows)
        {
            using var raw = JsonDocument.Parse(row.EvidenceExcerpt[4..]);
            var metrics = raw.RootElement.GetProperty("metrics");
            Assert.Equal("M", row.EvidenceClass);
            Assert.Equal(metrics.GetProperty("accuracy").GetDecimal(), row.Score);
            Assert.Equal(metrics.GetProperty("total_cost_usd").GetDecimal(), row.Context!.TotalCostUsd);
            Assert.Equal(metrics.GetProperty("n_trials").GetInt32(), row.Context.TrialCount);
            Assert.InRange(Math.Abs(row.SecondaryMetrics!.CostPerTaskUsd!.Value -
                row.Context.TotalCostUsd!.Value / row.Context.TrialCount!.Value), 0m, .000000001m);
            Assert.Equal("firstObservedPublicSnapshot", row.Context.DateBasis);
        }
        Assert.Contains(catalogue.Results, row => row.ModelId.Value == "claude-sonnet-5-5"
            && row.Score == 61.82m && row.PublishedAt == new DateOnly(2026, 11, 1));
        Assert.Contains(rows, row => row.ModelId.Value == "claude-sonnet-5-5" && row.Score == 63.94m);
    }

    [Fact]
    public void ArcRowsPreserveDatasetCostAndReasoningClassification()
    {
        var catalogue = BenchmarkEvidenceCatalog.Default;
        var rows = catalogue.Results.Where(row => row.Id.StartsWith("te82-arc2-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(46, rows.Length);
        foreach (var row in rows)
        {
            using var raw = JsonDocument.Parse(row.EvidenceExcerpt[4..]);
            Assert.Equal("v2_Semi_Private", raw.RootElement.GetProperty("datasetId").GetString());
            Assert.Equal(raw.RootElement.GetProperty("costPerTask").GetDecimal(), row.SecondaryMetrics!.CostPerTaskUsd);
            Assert.InRange(Math.Abs(raw.RootElement.GetProperty("score").GetDecimal() * 100 - row.Score), 0m, .000000000001m);
            Assert.Equal(BenchmarkCapabilityClass.Reasoning, catalogue.FindType(row.BenchmarkTypeId)!.CapabilityClass);
            Assert.Equal("M", row.EvidenceClass);
        }
    }

    [Fact]
    public void SubscriptionCreditsRemainSeparateFromApiTariffsAndMessageEntitlements()
    {
        var rows = SubscriptionCatalogue.Default.Observations;
        var rates = rows.Where(row => row.Kind == "credit-rate-card").ToArray();
        Assert.Equal(7, rates.Length);
        foreach (var row in rates)
        {
            var api = ModelPriceCatalog.Default.ResolvePrice(row.ModelId!, AsOf).Price!;
            Assert.Equal(api.InputPerMTok * 25, row.InputCreditsPerMTok);
            Assert.Equal(api.CacheReadPerMTok * 25, row.CacheReadCreditsPerMTok);
            Assert.Equal(api.OutputPerMTok * 25, row.OutputCreditsPerMTok);
            Assert.Equal("V", row.EvidenceClass);
            Assert.Equal("https://learn.chatgpt.com/docs/pricing", row.SourceUrl);
            var estimate = Assert.Single(rows, candidate => candidate.Kind == "message-range" && candidate.ModelId == row.ModelId);
            Assert.Equal("openai-plus", estimate.PlanId);
            Assert.True(estimate.MinimumMessages <= estimate.MaximumMessages);
            Assert.Contains("Estimated", estimate.Note);
        }
        Assert.Contains(rows, row => row.Kind == "vendor-statement" && row.Note.Contains("No vendor publishes", StringComparison.Ordinal));
    }
}
