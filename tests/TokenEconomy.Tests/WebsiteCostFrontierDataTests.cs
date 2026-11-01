using System.Text.Json;
using System.Text.RegularExpressions;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class WebsiteCostFrontierDataTests
{
    [Fact]
    [Trait("Category", "Guard")]
    public void FrontierCatalogHasNoPlaceholderModelsOrDuplicateObservations()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "benchmark-results.json")));
        var rows = document.RootElement.GetProperty("records").EnumerateArray()
            .Where(row => row.GetProperty("benchmarkTypeId").GetString()!.StartsWith("cost-frontier-", StringComparison.Ordinal))
            .ToArray();
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "cost-frontier-metadata.json")));
        var selected = metadata.RootElement.GetProperty("benchmarks").EnumerateObject()
            .SelectMany(benchmark => benchmark.Value.GetProperty("displayRowIds").EnumerateArray()
                .Concat(benchmark.Value.GetProperty("displayScoreOnlyIds").EnumerateArray()))
            .Select(id => id.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(selected.Order(), rows.Select(row => row.GetProperty("id").GetString()!).Order());
        Assert.DoesNotContain(metadata.RootElement.EnumerateObject(), property =>
            property.Name.Length > 0 && char.IsUpper(property.Name[0]));
        Assert.All(metadata.RootElement.GetProperty("externalModels").EnumerateObject(), model =>
            Assert.Contains(model.Value.GetProperty("vendor").GetString(),
                new[] { "xai", "deepseek", "alibaba", "moonshot", "zai", "xiaomi", "mistral" }));
        var external = metadata.RootElement.GetProperty("externalModels");
        foreach (var (model, vendor) in new Dictionary<string, string>
        {
            ["grok-4.7"] = "xai", ["ds-4.1f"] = "deepseek", ["qwen-3.8"] = "alibaba",
            ["kimi-3.1"] = "moonshot", ["glm-5.3"] = "zai", ["mimo-p"] = "xiaomi",
            ["mistral-4"] = "mistral",
        })
            Assert.Equal(vendor, external.GetProperty(model).GetProperty("vendor").GetString());
        Assert.All(metadata.RootElement.GetProperty("benchmarks").EnumerateObject(), benchmark =>
            Assert.Contains(benchmark.Value.GetProperty("cost").GetString(),
                new[] { "measured", "chartRead", "estimated" }));
        Assert.DoesNotContain(rows, row => row.GetProperty("modelId").GetString() == "unpriced-score-only");
        var observations = rows.Select(row => string.Join('|',
            row.GetProperty("benchmarkTypeId").GetString(), row.GetProperty("modelId").GetString(),
            row.GetProperty("reasoningEffort").GetString(), row.GetProperty("score").GetRawText(),
            row.TryGetProperty("settingLabel", out var setting) ? setting.GetString() : null,
            row.TryGetProperty("costSource", out var source) ? source.GetString() : null,
            row.GetProperty("sourceUrl").GetString(),
            row.TryGetProperty("secondaryMetrics", out var metrics) ? metrics.GetRawText() : null));
        Assert.Equal(rows.Length, observations.Distinct(StringComparer.Ordinal).Count());
        var sourcedRuns = rows.Select(row => string.Join('|',
            row.GetProperty("benchmarkTypeId").GetString(), row.GetProperty("modelId").GetString(),
            row.TryGetProperty("settingLabel", out var setting) ? setting.GetString() : row.GetProperty("reasoningEffort").GetString(),
            row.GetProperty("sourceUrl").GetString(), row.GetProperty("publishedAt").GetString()));
        Assert.Equal(rows.Length, sourcedRuns.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(rows.Length, rows.Select(row => row.GetProperty("evidenceExcerpt").GetString())
            .Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(rows, row => row.GetProperty("evidenceExcerpt").GetString()!.Contains("Sorry, you have been blocked", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.GetProperty("evidenceExcerpt").GetString()!.Contains("VIEW BRAND KIT", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.GetProperty("evidenceExcerpt").GetString()!.Contains("TERMINAL-BENCH RUN BENCHMARKS BLOG", StringComparison.Ordinal));
        Assert.All(rows.Where(row => row.GetProperty("costSource").GetString() == "unavailable"), row =>
        {
            Assert.True(row.TryGetProperty("settingLabel", out _));
            Assert.True(row.TryGetProperty("scoreOnlyLabel", out _));
            Assert.False(row.TryGetProperty("scoreOnlyModelId", out _));
        });
        Assert.All(rows.Where(row => row.GetProperty("costSource").GetString() == "chartRead"), row =>
        {
            var excerpt = row.GetProperty("evidenceExcerpt").GetString()!;
            Assert.Contains(row.GetProperty("modelId").GetString()!, excerpt, StringComparison.Ordinal);
            Assert.Contains(row.GetProperty("settingLabel").GetString()!, excerpt, StringComparison.Ordinal);
            Assert.Contains(row.GetProperty("score").GetRawText(), excerpt, StringComparison.Ordinal);
        });
        var astra = Assert.Single(rows, row => row.GetProperty("id").GetString() == "cf-exploit-001");
        Assert.Equal("https://openai.com/index/gpt-6-astra/", astra.GetProperty("sourceUrl").GetString());
        Assert.Contains("100%", astra.GetProperty("evidenceExcerpt").GetString());
        var arcRows = rows.Where(row => row.GetProperty("benchmarkTypeId").GetString() == "cost-frontier-arc2").ToArray();
        Assert.Equal(49, arcRows.Length);
        Assert.All(arcRows, row =>
        {
            using var source = JsonDocument.Parse(row.GetProperty("evidenceExcerpt").GetString()!);
            var excerpt = source.RootElement;
            Assert.Equal("v2_Semi_Private", excerpt.GetProperty("datasetId").GetString());
            Assert.InRange(Math.Abs(row.GetProperty("score").GetDecimal()
                - excerpt.GetProperty("score").GetDecimal() * 100m), 0m, 0.05m);
            Assert.Equal(row.GetProperty("secondaryMetrics").GetProperty("costPerTaskUsd").GetDecimal(),
                decimal.Round(excerpt.GetProperty("costPerTask").GetDecimal(), 4));
            Assert.Contains(row.GetProperty("modelId").GetString()!.Replace('.', '-'),
                excerpt.GetProperty("modelId").GetString()!, StringComparison.Ordinal);
        });
        var presenc = Assert.Single(rows, row => row.GetProperty("id").GetString() == "cf-osw2-gap-001");
        Assert.Contains("GPT-6 Astra | 72.6%", presenc.GetProperty("evidenceExcerpt").GetString());
        var sol = Assert.Single(rows, row => row.GetProperty("id").GetString() == "cf-scoreonly-osw2-002");
        Assert.Contains("GPT-6.1 Sol 71.4%", sol.GetProperty("evidenceExcerpt").GetString());
        using var types = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "benchmark-types.json")));
        foreach (var type in types.RootElement.GetProperty("records").EnumerateArray()
            .Where(type => type.GetProperty("id").GetString() is "cost-frontier-arena" or "cost-frontier-gdpval"))
        {
            var highest = rows.Where(row => row.GetProperty("benchmarkTypeId").GetString() == type.GetProperty("id").GetString())
                .Max(row => row.GetProperty("score").GetDecimal());
            Assert.True(type.GetProperty("maximumScore").GetDecimal() > highest);
        }
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
        var benchmark = document.RootElement.GetProperty("benchmarks").GetProperty("aacode");
        Assert.Contains(benchmark.GetProperty("noCost").EnumerateArray(),
            row => row[0].GetString() == "Claude Fable 5.1" && row[1].GetDecimal() == 58m);
        Assert.All(benchmark.GetProperty("scoreOnlyEvidence").EnumerateArray(),
            row => Assert.Equal("unavailable", row.GetProperty("costSource").GetString()));
        Assert.DoesNotContain(document.RootElement.GetProperty("benchmarks").GetProperty("osw2")
            .GetProperty("noCost").EnumerateArray(),
            row => row[0].GetString()!.Contains("Haiku 5.5", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishedExplorerKeepsProvenanceAndCatalogPricesSeparate()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "cost-frontier.json")));
        var root = document.RootElement;
        var models = root.GetProperty("models");
        var ids = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "src", "TokenEconomy", "catalog", "cost-frontier-metadata.json")))
            .RootElement.GetProperty("modelIds").Clone();
        var asOf = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(8, root.GetProperty("categories").GetArrayLength());
        Assert.Equal(16, root.GetProperty("insights").GetArrayLength());
        Assert.Equal(10, root.GetProperty("missions").GetArrayLength());
        Assert.Equal(2, root.GetProperty("missions").EnumerateArray()
            .Count(item => item.TryGetProperty("check", out var check) && check.GetBoolean()));
        Assert.Equal(18, root.GetProperty("releases").GetArrayLength());
        Assert.Equal(15, root.GetProperty("benchmarks").EnumerateObject()
            .Sum(item => item.Value.GetProperty("noCost").GetArrayLength()));
        Assert.True(root.GetProperty("restricted").TryGetProperty("carbon", out _));

        var estimatedWithCatalogPrice = 0;
        foreach (var benchmark in root.GetProperty("benchmarks").EnumerateObject())
        {
            var value = benchmark.Value;
            foreach (var fact in value.GetProperty("scoreOnlyEvidence").EnumerateArray())
                AssertEvidenceAge(fact);
            var expectedSource = value.GetProperty("cost").GetString() switch
            {
                "measured" => "measured",
                "chartRead" => "chartRead",
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
    [Trait("Category", "Guard")]
    public void SupersededModelsCannotReachAnyBenchmarkOrMission()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "cost-frontier.json")));
        var root = document.RootElement;
        var current = root.GetProperty("current").EnumerateArray().Select(value => value.GetString()!).ToHashSet();
        var models = root.GetProperty("models").EnumerateObject().Select(value => value.Name).ToHashSet();
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
        foreach (var benchmark in root.GetProperty("benchmarks").EnumerateObject())
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
                Assert.Contains(catalogRow.GetProperty("modelId").GetString()!, canonicalCurrent);
            }
        }
        foreach (var mission in root.GetProperty("missions").EnumerateArray())
        {
            if (!mission.TryGetProperty("opts", out var options)) continue;
            foreach (var option in options.EnumerateArray()) Assert.Contains(option[0].GetString()!, current);
        }
        foreach (var insight in root.GetProperty("insights").EnumerateArray())
        {
            if (insight.GetProperty("on").TryGetProperty("m", out var model))
                Assert.Contains(model.GetString()!, current);
        }
        foreach (var release in root.GetProperty("releases").EnumerateArray())
            foreach (var model in release.GetProperty("ms").EnumerateArray())
                Assert.Contains(model.GetString()!, current);
        var storyText = root.GetProperty("insights").ToString() + root.GetProperty("missions").ToString();
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
        var row = Assert.Single(root.GetProperty("benchmarks").GetProperty(benchmark).GetProperty("rows").EnumerateArray(),
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
