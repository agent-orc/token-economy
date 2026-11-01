using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class WebsiteCostFrontierDataTests
{
    [Fact]
    public void ChartReadCostsNeverAppearAsPublishedMeasurements()
    {
        var matrix = ModelBenchmarkMatrix.Default.Build("cost-frontier-osw2",
            new BenchmarkTokenAssumption(100_000, 10_000), null,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var chartReadCells = matrix.Cells.Where(cell => cell.CostBasis == BenchmarkCostBasis.ChartRead).ToArray();
        Assert.NotEmpty(chartReadCells);
        Assert.All(chartReadCells, cell =>
        {
            Assert.Null(cell.PublishedCostPerTaskUsd);
            Assert.NotNull(cell.ChartReadCostPerTaskUsd);
        });
        Assert.All(matrix.Cells, cell => Assert.True(
            cell.CostBasis is BenchmarkCostBasis.ChartRead or BenchmarkCostBasis.Unavailable));
    }

    [Fact]
    public void ScoreOnlyRowsNeverAcquireAnEstimatedCost()
    {
        var matrix = ModelBenchmarkMatrix.Default.Build("cost-frontier-arc2",
            new BenchmarkTokenAssumption(100_000, 10_000), null,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var scoreOnly = Assert.Single(matrix.Cells, cell =>
            cell.Key.ModelId.Value == "claude-opus-5-5" && cell.Key.Effort == EffortLevel.Max);
        Assert.Equal(BenchmarkCostBasis.Unavailable, scoreOnly.CostBasis);
        Assert.Null(scoreOnly.CostPerTaskUsd);
    }

    [Fact]
    public void PublishedExplorerKeepsProvenanceAndCatalogPricesSeparate()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "cost-frontier.json")));
        var root = document.RootElement;
        var models = root.GetProperty("M");
        var ids = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "cost-frontier-metadata.json")))
            .RootElement.GetProperty("modelIds").Clone();
        var asOf = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(9, root.GetProperty("CATS").GetArrayLength());
        Assert.Equal(14, root.GetProperty("INSIGHTS").GetArrayLength());
        Assert.Equal(10, root.GetProperty("MISSIONS").GetArrayLength());
        Assert.Equal(2, root.GetProperty("MISSIONS").EnumerateArray()
            .Count(item => item.TryGetProperty("check", out var check) && check.GetBoolean()));
        Assert.Equal(25, root.GetProperty("RELEASES").GetArrayLength());
        Assert.Equal(26, root.GetProperty("B").EnumerateObject()
            .Sum(item => item.Value.GetProperty("noCost").GetArrayLength()));
        Assert.True(root.GetProperty("RESTRICTED").TryGetProperty("carbon", out _));

        var estimatedWithCatalogPrice = 0;
        foreach (var benchmark in root.GetProperty("B").EnumerateObject())
        {
            var value = benchmark.Value;
            foreach (var fact in value.GetProperty("scoreOnlyEvidence").EnumerateArray())
                AssertEvidenceAge(fact);
            var expectedSource = value.GetProperty("cost").GetString() switch
            {
                "measured" => "measured",
                "chartread" => "chartRead",
                _ => "estimated",
            };
            var rows = value.GetProperty("rows").EnumerateArray().ToArray();
            var evidence = value.GetProperty("evidence").EnumerateArray().ToArray();
            Assert.Equal(rows.Length, evidence.Length);
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var fact = evidence[index];
                AssertEvidenceAge(fact);
                Assert.Equal(expectedSource, fact.GetProperty("costSource").GetString());
                Assert.StartsWith("http", fact.GetProperty("sourceUrl").GetString()!);
                Assert.False(string.IsNullOrWhiteSpace(fact.GetProperty("evidenceExcerpt").GetString()));
                if (expectedSource != "estimated")
                {
                    Assert.Equal(4, row.GetArrayLength());
                    continue;
                }
                Assert.Equal(3, row.GetArrayLength());
                var shortId = row[0].GetString()!;
                var catalogId = ids.GetProperty(shortId).GetString()!;
                var price = ModelPriceCatalog.Default.ResolvePrice(catalogId, asOf);
                if (price.Price is null) continue;
                var usage = new TokenUsage(100_000, 10_000, PromptTokens: 100_000);
                var cost = ModelPriceCatalog.Default.ComputeCost(catalogId, usage, asOf);
                Assert.True(cost.HasPrice);
                var displayed = models.GetProperty(shortId);
                var reconstructed = (displayed[2].GetDecimal() * 100_000m + displayed[3].GetDecimal() * 10_000m) / 1_000_000m;
                Assert.Equal(reconstructed, cost.Total!.Value);
                estimatedWithCatalogPrice++;
            }
        }
        Assert.True(estimatedWithCatalogPrice > 20);
        AssertSpot(root, "aa5", "opus-5.5", "max", 58m, 10.04m);
        AssertSpot(root, "aa5", "sol-6.1", "max", 55m, 1.27m);
        AssertSpot(root, "deepswe", "flash-3.8", "high", 74m, 2.36m);
        AssertSpot(root, "deepswe", "sonnet-5", "max", 54m, 26.40m);
    }

    private static void AssertEvidenceAge(JsonElement fact)
    {
        var published = DateOnly.Parse(fact.GetProperty("publishedAt").GetString()![..10]);
        Assert.Equal(new DateOnly(2026, 11, 1).DayNumber - published.DayNumber > 90,
            fact.GetProperty("stale").GetBoolean());
    }

    private static void AssertSpot(JsonElement root, string benchmark, string model, string setting, decimal score, decimal cost)
    {
        var row = Assert.Single(root.GetProperty("B").GetProperty(benchmark).GetProperty("rows").EnumerateArray(),
            item => item[0].GetString() == model && item[1].GetString() == setting);
        Assert.Equal(score, row[2].GetDecimal());
        Assert.Equal(cost, row[3].GetDecimal());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
