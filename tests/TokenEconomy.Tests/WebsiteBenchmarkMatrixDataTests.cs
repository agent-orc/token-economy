using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class WebsiteBenchmarkMatrixDataTests
{
    [Fact]
    public void PublishedMatrixMatchesPureApiAndCarriesThreeDatedProvenanceFacts()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "model-benchmark-matrix.json")));
        var root = document.RootElement;
        var asOf = root.GetProperty("asOfUtc").GetDateTime().ToUniversalTime();
        var assumptionJson = root.GetProperty("tokenAssumption");
        var assumption = new BenchmarkTokenAssumption(
            assumptionJson.GetProperty("input").GetInt64(),
            assumptionJson.GetProperty("output").GetInt64(),
            assumptionJson.GetProperty("cacheRead").GetInt64(),
            assumptionJson.GetProperty("cacheWrite").GetInt64());

        Assert.Equal(3, root.GetProperty("provenanceFacts").GetArrayLength());
        foreach (var fact in root.GetProperty("provenanceFacts").EnumerateArray())
        {
            Assert.True(DateOnly.TryParse(fact.GetProperty("date").GetString(), out _));
            Assert.True(File.Exists(Path.Combine(RepositoryRoot(), fact.GetProperty("path").GetString()!)));
        }

        foreach (var publishedType in root.GetProperty("benchmarkTypes").EnumerateArray())
        {
            var typeId = publishedType.GetProperty("id").GetString()!;
            BenchmarkCellKey? reference = null;
            if (publishedType.GetProperty("reference").ValueKind != JsonValueKind.Null)
            {
                var item = publishedType.GetProperty("reference");
                reference = new(ModelId.Of(item.GetProperty("modelId").GetString()!),
                    Enum.Parse<EffortLevel>(item.GetProperty("effort").GetString()!, ignoreCase: true));
            }
            var expected = ModelBenchmarkMatrix.Default.Build(typeId, assumption, reference, asOf);
            var actual = publishedType.GetProperty("cells").EnumerateArray().ToArray();

            Assert.Equal(expected.Cells.Count, actual.Length);
            foreach (var cell in expected.Cells)
            {
                var row = Assert.Single(actual, item =>
                    item.GetProperty("modelId").GetString() == cell.Key.ModelId.Value
                    && string.Equals(item.GetProperty("effort").GetString(),
                        JsonNamingPolicy.CamelCase.ConvertName(cell.Key.Effort.ToString()), StringComparison.Ordinal));
                Assert.Equal(cell.Score, row.GetProperty("score").GetDecimal());
                Assert.Equal(cell.CostBasis.ToString(), row.GetProperty("costBasis").GetString(), ignoreCase: true);
                Assert.Equal(cell.EvidenceAgeDays, row.GetProperty("evidenceAgeDays").GetInt32());
                Assert.Equal(cell.IsStale, row.GetProperty("stale").GetBoolean());
                Assert.Equal(cell.Evidence.Count, row.GetProperty("evidence").GetArrayLength());
                AssertNullable(cell.CostPerTaskUsd, row.GetProperty("costPerTaskUsd"));
                AssertNullable(cell.ScoreDeltaToReference, row.GetProperty("scoreDeltaToReference"));
                AssertNullable(cell.CostDeltaToReferenceUsd, row.GetProperty("costDeltaToReferenceUsd"));
            }
        }
    }

    [Fact]
    public void PublishedDrivingAnswerIsDerivedAndDoesNotCallAstraLowCheaper()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "website", "data", "model-benchmark-matrix.json")));
        var answer = document.RootElement.GetProperty("drivingQuestion");

        Assert.True(answer.GetProperty("better").GetBoolean());
        Assert.False(answer.GetProperty("cheaper").GetBoolean());
        Assert.Contains("not cheaper", answer.GetProperty("answer").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNullable(decimal? expected, JsonElement actual)
    {
        if (expected is null)
            Assert.Equal(JsonValueKind.Null, actual.ValueKind);
        else
            Assert.Equal(Math.Round(expected.Value, 6), actual.GetDecimal());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
