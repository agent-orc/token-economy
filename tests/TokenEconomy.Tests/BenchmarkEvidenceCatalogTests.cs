using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class BenchmarkEvidenceCatalogTests
{
    [Fact]
    public void DefaultCatalog_LoadsVersionedTypesAndTypedKnownModels()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Contains(catalog.Types, type => type.Id == "swe-bench-verified-500");
        Assert.Contains(catalog.Types, type => type.Id == "token-economy-controlled-setups-v1");
        Assert.All(catalog.Results, result =>
        {
            if (ModelPriceCatalog.Default.Find(result.ModelId) is null)
                Assert.Contains(result.ModelId.Value, catalog.FindType(result.BenchmarkTypeId)!.AllowedUnpricedModelIds);
            Assert.NotEqual(default, result.RetrievedAt);
            Assert.True(Uri.TryCreate(result.SourceUrl, UriKind.Absolute, out _));
            Assert.False(string.IsNullOrWhiteSpace(result.EvidenceExcerpt));
        });
    }

    [Fact]
    public void ScoreOnlyModelAndCostDerivationSurviveCatalogLoading()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var scoreOnly = Assert.Single(catalog.Results, result => result.Id == "cf-aacode-gap-001");
        Assert.Equal("claude-fable-5-1", scoreOnly.ModelId.Value);
        Assert.Equal("Claude Fable 5.1", scoreOnly.ScoreOnlyLabel);

        var derived = Assert.Single(catalog.Results, result => result.Id == "cf-tb4-001");
        Assert.Contains("total_cost_usd / n_trials", derived.CostDerivation);
    }

    [Fact]
    public void SchemasRequireRetrievalDateAndSourceAndDocumentsDeclareSchemas()
    {
        var root = RepositoryRoot();
        using var resultSchema = Read(root, "src/TokenEconomy/catalog/benchmark-results.schema.json");
        using var typeSchema = Read(root, "src/TokenEconomy/catalog/benchmark-types.schema.json");
        using var results = Read(root, "src/TokenEconomy/catalog/benchmark-results.json");
        using var types = Read(root, "src/TokenEconomy/catalog/benchmark-types.json");
        var required = resultSchema.RootElement.GetProperty("$defs").GetProperty("result")
            .GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.Contains("retrievedAt", required);
        Assert.Contains("sourceUrl", required);
        Assert.Equal("benchmark-results.schema.json", results.RootElement.GetProperty("$schema").GetString());
        Assert.Equal("benchmark-types.schema.json", types.RootElement.GetProperty("$schema").GetString());
        Assert.Equal(1, resultSchema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
        Assert.Equal(1, typeSchema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
    }

    [Fact]
    public void ConstructorRejectsMissingSourceUnknownModelsAndOutOfRangeScores()
    {
        var prices = Prices(("model-a", 1m));
        var type = Type("quality");

        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [Result("bad-source", "quality", "model-a", EffortLevel.Low, 50m) with { SourceUrl = "" }], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [Result("bad-retrieval", "quality", "model-a", EffortLevel.Low, 50m) with { RetrievedAt = default }], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [Result("bad-model", "quality", "missing", EffortLevel.Low, 50m)], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [Result("bad-score", "quality", "model-a", EffortLevel.Low, 101m)], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type with { AllowedUnpricedModelIds = ["model-b"] }],
            [Result("bad-model", "quality", "model-c", EffortLevel.Low, 50m)], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type with { AllowedUnpricedModelIds = ["model-b"] }],
            [Result("unused-exception", "quality", "model-a", EffortLevel.Low, 50m)], prices));
    }

    [Fact]
    public void CostFrontierRejectsContradictoryCostAndScoreOnlyFields()
    {
        var prices = Prices(("model-a", 1m));
        var type = Type("cost-frontier-fixture");
        var row = Result("fixture", type.Id, "model-a", EffortLevel.Low, 50m)
            with { SettingLabel = "low" };
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [row with { CostSource = BenchmarkCostSource.Unavailable }], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [row with { CostSource = BenchmarkCostSource.Measured }], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [row with { CostSource = BenchmarkCostSource.Estimated,
                SecondaryMetrics = new() { CostPerTaskUsd = 1m } }], prices));
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            [type], [row with { CostSource = BenchmarkCostSource.Unavailable,
                ScoreOnlyLabel = "Model A", SecondaryMetrics = new() { CostPerTaskUsd = 1m } }], prices));
    }

    internal static BenchmarkType Type(string id, BenchmarkScoreDirection direction = BenchmarkScoreDirection.HigherIsBetter) => new()
    {
        Id = id,
        Version = "1",
        Name = id,
        Publisher = "fixture",
        CapabilityClass = BenchmarkCapabilityClass.Reasoning,
        Unit = "points",
        MinimumScore = 0,
        MaximumScore = 100,
        Direction = direction,
        MethodologyUrl = "https://example.test/method",
        CitationNote = "fixture",
        ValidFrom = new DateOnly(2026, 1, 1),
        CapturedAt = new DateOnly(2026, 1, 1),
        RetrievedAt = new DateOnly(2026, 1, 1),
    };

    internal static BenchmarkResult Result(
        string id, string type, string model, EffortLevel effort, decimal score,
        DateOnly? published = null, decimal? cost = null) => new()
    {
        Id = id,
        BenchmarkTypeId = type,
        ModelId = ModelId.Of(model),
        ReasoningEffort = effort,
        Score = score,
        SecondaryMetrics = cost is null ? null : new() { CostPerTaskUsd = cost },
        PublishedAt = published ?? new DateOnly(2026, 1, 1),
        RetrievedAt = published ?? new DateOnly(2026, 1, 1),
        SourceUrl = $"https://example.test/{id}",
        RetrievalMethod = BenchmarkRetrievalMethod.Manual,
        EvidenceExcerpt = id,
        Confidence = BenchmarkConfidence.PublisherReported,
    };

    internal static ModelPriceCatalog Prices(params (string Id, decimal Input)[] models) => new(
        models.Select(model => new ModelListing
        {
            ModelId = model.Id,
            History =
            [
                new ModelPrice
                {
                    InputPerMTok = model.Input,
                    OutputPerMTok = model.Input,
                    ValidFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                },
            ],
        }));

    private static JsonDocument Read(string root, string relative) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
