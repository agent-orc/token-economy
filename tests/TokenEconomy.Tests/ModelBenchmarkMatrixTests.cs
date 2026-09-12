using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelBenchmarkMatrixTests
{
    private static readonly DateTime AsOf = new(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly BenchmarkTokenAssumption Assumption = new(1_000_000, 0);

    [Fact]
    public void MatrixUsesPublishedCostOtherwiseDeclaredTokensAndNeverDuplicatesPriceEvidence()
    {
        var prices = BenchmarkEvidenceCatalogTests.Prices(("model-a", 4m), ("model-b", 2m));
        var evidence = new BenchmarkEvidenceCatalog(
            [BenchmarkEvidenceCatalogTests.Type("quality")],
            [
                BenchmarkEvidenceCatalogTests.Result("a", "quality", "model-a", EffortLevel.High, 80m, cost: 3m),
                BenchmarkEvidenceCatalogTests.Result("b", "quality", "model-b", EffortLevel.Low, 81m),
            ], prices);

        var result = new ModelBenchmarkMatrix(evidence, prices).Build(
            "quality", Assumption, new(ModelId.Of("model-a"), EffortLevel.High), AsOf);
        var a = Assert.Single(result.Cells, cell => cell.Key.ModelId.Value == "model-a");
        var b = Assert.Single(result.Cells, cell => cell.Key.ModelId.Value == "model-b");

        Assert.Equal(BenchmarkCostBasis.PublishedPerTask, a.CostBasis);
        Assert.Equal(3m, a.CostPerTaskUsd);
        Assert.Equal(BenchmarkCostBasis.DeclaredTokenAssumption, b.CostBasis);
        Assert.Equal(2m, b.CostPerTaskUsd);
        Assert.Equal(2m, b.Prices.InputPerMTok);
        Assert.Equal(1m, b.ScoreDeltaToReference);
        Assert.Equal(-1m, b.CostDeltaToReferenceUsd);
        Assert.Equal(40.5m, b.ScorePerDollar);
    }

    [Fact]
    public void WeightedMatrixNormalizesDirectionAndRequiresCompleteCells()
    {
        var prices = BenchmarkEvidenceCatalogTests.Prices(("model-a", 1m), ("model-b", 1m));
        var types = new[]
        {
            BenchmarkEvidenceCatalogTests.Type("higher"),
            BenchmarkEvidenceCatalogTests.Type("lower", BenchmarkScoreDirection.LowerIsBetter),
        };
        var evidence = new BenchmarkEvidenceCatalog(types,
        [
            BenchmarkEvidenceCatalogTests.Result("a-high", "higher", "model-a", EffortLevel.Low, 80m),
            BenchmarkEvidenceCatalogTests.Result("a-low", "lower", "model-a", EffortLevel.Low, 20m),
            BenchmarkEvidenceCatalogTests.Result("b-only", "higher", "model-b", EffortLevel.Low, 100m),
        ], prices);

        var matrix = new ModelBenchmarkMatrix(evidence, prices).Build(
            [new() { BenchmarkTypeId = "higher", Weight = 3m }, new() { BenchmarkTypeId = "lower", Weight = 1m }],
            Assumption, null, AsOf);

        var cell = Assert.Single(matrix.Cells);
        Assert.True(cell.ScoreIsNormalized);
        Assert.Equal(80m, cell.Score);
        Assert.Equal(2, cell.ScoresByBenchmark.Count);
    }

    [Fact]
    public void CandidateQueryRequiresAtLeastEqualScoreAndCheaperCostAndRanksQualityFirst()
    {
        var prices = BenchmarkEvidenceCatalogTests.Prices(
            ("current", 5m), ("better", 2m), ("best", 3m), ("cheap-worse", 1m), ("better-expensive", 6m));
        var evidence = new BenchmarkEvidenceCatalog(
            [BenchmarkEvidenceCatalogTests.Type("quality")],
            [
                BenchmarkEvidenceCatalogTests.Result("current", "quality", "current", EffortLevel.High, 80m),
                BenchmarkEvidenceCatalogTests.Result("better", "quality", "better", EffortLevel.Low, 82m),
                BenchmarkEvidenceCatalogTests.Result("best", "quality", "best", EffortLevel.Low, 90m),
                BenchmarkEvidenceCatalogTests.Result("cheap-worse", "quality", "cheap-worse", EffortLevel.Low, 79m),
                BenchmarkEvidenceCatalogTests.Result("better-expensive", "quality", "better-expensive", EffortLevel.Low, 95m),
            ], prices);
        var matrix = new ModelBenchmarkMatrix(evidence, prices);

        var candidates = matrix.FindCandidates(
            new(ModelId.Of("current"), EffortLevel.High), "quality", Assumption, AsOf);

        Assert.Equal(["best", "better"], candidates.Select(candidate => candidate.Cell.Key.ModelId.Value));
        Assert.All(candidates, candidate => Assert.NotEmpty(candidate.Evidence));
    }

    [Fact]
    public void EvidenceOlderThanNinetyDaysIsStaleAndNewerEvidenceIsFresh()
    {
        var prices = BenchmarkEvidenceCatalogTests.Prices(("old", 1m), ("fresh", 1m));
        var evidence = new BenchmarkEvidenceCatalog(
            [BenchmarkEvidenceCatalogTests.Type("quality")],
            [
                BenchmarkEvidenceCatalogTests.Result("old", "quality", "old", EffortLevel.Low, 50m, new DateOnly(2026, 1, 1)),
                BenchmarkEvidenceCatalogTests.Result("fresh", "quality", "fresh", EffortLevel.Low, 50m, new DateOnly(2026, 4, 1)),
            ], prices);

        var cells = new ModelBenchmarkMatrix(evidence, prices).Build("quality", Assumption, null, AsOf).Cells;

        Assert.True(Assert.Single(cells, cell => cell.Key.ModelId.Value == "old").IsStale);
        Assert.Equal(99, Assert.Single(cells, cell => cell.Key.ModelId.Value == "old").EvidenceAgeDays);
        Assert.False(Assert.Single(cells, cell => cell.Key.ModelId.Value == "fresh").IsStale);
    }

    [Fact]
    public void DefaultMatrixAnswersDrivingQuestionWithoutCallingItACandidate()
    {
        var matrix = ModelBenchmarkMatrix.Default;
        var reference = new BenchmarkCellKey(KnownModels.Gpt56Sol, EffortLevel.High);
        var result = matrix.Build(
            "artificial-analysis-intelligence-index-v4.3",
            new BenchmarkTokenAssumption(100_000, 10_000), reference,
            new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));
        var astra = Assert.Single(result.Cells, cell => cell.Key == new BenchmarkCellKey(KnownModels.Gpt6Astra, EffortLevel.Low));
        var sol = Assert.Single(result.Cells, cell => cell.Key == reference);

        Assert.True(astra.Score > sol.Score);
        Assert.True(astra.CostPerTaskUsd > sol.CostPerTaskUsd);
        Assert.DoesNotContain(matrix.FindCandidates(reference, "artificial-analysis-intelligence-index-v4.3",
            new BenchmarkTokenAssumption(100_000, 10_000), result.AsOfUtc),
            candidate => candidate.Cell.Key == astra.Key);
    }
}
