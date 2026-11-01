using System.Text.Json;
using System.Text.RegularExpressions;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class WebsiteCostFrontierDataTests
{
    [Fact]
    public void FrontierCatalogHasNoPlaceholderModelsOrDuplicateObservations()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "benchmark-results.json")));
        var rows = document.RootElement.GetProperty("records").EnumerateArray()
            .Where(row => row.GetProperty("benchmarkTypeId").GetString()!.StartsWith("cost-frontier-", StringComparison.Ordinal))
            .ToArray();
        Assert.DoesNotContain(rows, row => row.GetProperty("modelId").GetString() == "unpriced-score-only");
        var observations = rows.Select(row => string.Join('|',
            row.GetProperty("benchmarkTypeId").GetString(), row.GetProperty("modelId").GetString(),
            row.GetProperty("reasoningEffort").GetString(), row.GetProperty("score").GetRawText(),
            row.TryGetProperty("settingLabel", out var setting) ? setting.GetString() : null,
            row.TryGetProperty("costSource", out var source) ? source.GetString() : null,
            row.GetProperty("sourceUrl").GetString(),
            row.TryGetProperty("secondaryMetrics", out var metrics) ? metrics.GetRawText() : null));
        Assert.Equal(rows.Length, observations.Distinct(StringComparer.Ordinal).Count());
    }

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
    public void ScoreOnlyRowsRemainUnpricedInTheGeneratedExplorer()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "cost-frontier.json")));
        var benchmark = document.RootElement.GetProperty("B").GetProperty("aacode");
        Assert.Contains(benchmark.GetProperty("noCost").EnumerateArray(),
            row => row[0].GetString() == "Claude Fable 5.1" && row[1].GetDecimal() == 58m);
        Assert.All(benchmark.GetProperty("scoreOnlyEvidence").EnumerateArray(),
            row => Assert.Equal("unavailable", row.GetProperty("costSource").GetString()));
        Assert.DoesNotContain(document.RootElement.GetProperty("B").GetProperty("osw2")
            .GetProperty("noCost").EnumerateArray(),
            row => row[0].GetString()!.Contains("Haiku 5.5", StringComparison.Ordinal));
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
        Assert.Equal(8, root.GetProperty("CATS").GetArrayLength());
        Assert.Equal(16, root.GetProperty("INSIGHTS").GetArrayLength());
        Assert.Equal(10, root.GetProperty("MISSIONS").GetArrayLength());
        Assert.Equal(2, root.GetProperty("MISSIONS").EnumerateArray()
            .Count(item => item.TryGetProperty("check", out var check) && check.GetBoolean()));
        Assert.Equal(18, root.GetProperty("RELEASES").GetArrayLength());
        Assert.Equal(16, root.GetProperty("B").EnumerateObject()
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
        AssertSpot(root, "tb4", "sol-6.1", "max", 58.18m, 1.92m);
        AssertSpot(root, "tb4", "opus-5.5", "max", 64.85m, 14.30m);
        AssertSpot(root, "arc2", "sol-6.1", "max", 94.2m, 0.2537m);
        AssertSpot(root, "aacode", "opus-5.5", "max", 62m, 11.42m);
    }

    [Fact]
    public void SupersededModelsCannotReachAnyBenchmarkOrMission()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "cost-frontier.json")));
        var root = document.RootElement;
        var current = root.GetProperty("CURRENT").EnumerateArray().Select(value => value.GetString()!).ToHashSet();
        var models = root.GetProperty("M").EnumerateObject().Select(value => value.Name).ToHashSet();
        Assert.Equal(current.Order(), models.Order());
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "cost-frontier-metadata.json")));
        using var results = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "benchmark-results.json")));
        using var prices = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "model-prices.json")));
        var canonicalCurrent = current.Select(id => metadata.RootElement.GetProperty("modelIds").GetProperty(id).GetString()!).ToHashSet();
        var catalogRows = results.RootElement.GetProperty("records").EnumerateArray()
            .ToDictionary(row => row.GetProperty("id").GetString()!);
        var used = new HashSet<string>();
        foreach (var benchmark in root.GetProperty("B").EnumerateObject())
        {
            foreach (var row in benchmark.Value.GetProperty("rows").EnumerateArray())
            {
                var model = row[0].GetString()!;
                Assert.Contains(model, current);
                used.Add(model);
            }
        }
        foreach (var benchmark in metadata.RootElement.GetProperty("benchmarks").EnumerateObject())
        {
            foreach (var rowId in benchmark.Value.GetProperty("displayScoreOnlyIds").EnumerateArray())
            {
                var catalogRow = catalogRows[rowId.GetString()!];
                Assert.Contains(catalogRow.GetProperty("scoreOnlyModelId").GetString()!, canonicalCurrent);
            }
        }
        foreach (var mission in root.GetProperty("MISSIONS").EnumerateArray())
        {
            if (!mission.TryGetProperty("opts", out var options)) continue;
            foreach (var option in options.EnumerateArray()) Assert.Contains(option[0].GetString()!, current);
        }
        foreach (var insight in root.GetProperty("INSIGHTS").EnumerateArray())
        {
            if (insight.GetProperty("on").TryGetProperty("m", out var model))
                Assert.Contains(model.GetString()!, current);
        }
        foreach (var release in root.GetProperty("RELEASES").EnumerateArray())
            foreach (var model in release.GetProperty("ms").EnumerateArray())
                Assert.Contains(model.GetString()!, current);
        var storyText = root.GetProperty("INSIGHTS").ToString() + root.GetProperty("MISSIONS").ToString();
        var supersededNames = prices.RootElement.EnumerateArray()
            .Where(model => model.TryGetProperty("lifecycleStatus", out var lifecycle) && lifecycle.GetString() == "superseded")
            .Select(model => model.GetProperty("displayName").GetString()!)
            .Concat(metadata.RootElement.GetProperty("externalModels").EnumerateObject()
                .Where(model => model.Value.GetProperty("lifecycleStatus").GetString() == "superseded")
                .Select(model => model.Value.GetProperty("displayName").GetString()!));
        foreach (var name in supersededNames)
            Assert.False(Regex.IsMatch(storyText, Regex.Escape(name) + "(?![.\\dA-Za-z])", RegexOptions.IgnoreCase),
                $"Superseded model appears in mission or insight: {name}");
        Assert.DoesNotContain("sol-6", models);
        Assert.DoesNotContain("sonnet-5", models);
        Assert.DoesNotContain("grok-4.6", models);
        Assert.True(used.Count >= 15);
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
