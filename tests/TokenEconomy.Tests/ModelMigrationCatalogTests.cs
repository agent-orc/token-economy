using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelMigrationCatalogTests
{
    private static readonly DateTime AsOf = new(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MachineDocument_DeclaresItsVersionedSchema()
    {
        using var catalog = ReadJson("src/TokenEconomy/catalog/model-migrations.v1.json");
        using var schema = ReadJson("src/TokenEconomy/catalog/model-migrations.v1.schema.json");

        Assert.Equal("model-migrations.v1.schema.json", catalog.RootElement.GetProperty("$schema").GetString());
        Assert.Equal(1, catalog.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32(),
            catalog.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("latestInFamily", catalog.RootElement.GetProperty("defaultStrategy").GetString());
    }

    [Fact]
    public void EverySafeAutomaticMigration_SatisfiesThePublishedGates()
    {
        using var catalog = ReadJson("src/TokenEconomy/catalog/model-migrations.v1.json");
        var costRank = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["economy"] = 0,
            ["standard"] = 1,
            ["premium"] = 2,
        };

        foreach (var migration in catalog.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var from = migration.GetProperty("from").GetString()!;
            var to = migration.GetProperty("to").GetString()!;
            var family = migration.GetProperty("family").GetString()!;
            var evidence = migration.GetProperty("evidence");
            var safeAuto = migration.GetProperty("safeAuto").GetBoolean();

            if (evidence.ValueKind == JsonValueKind.Object)
            {
                var reference = evidence.GetProperty("reference").GetString()!;
                Assert.True(File.Exists(Path.Combine(RepositoryRoot(), reference)), $"Missing evidence: {reference}");
            }
            else
            {
                Assert.Equal("none", evidence.GetString());
            }

            if (!safeAuto)
                continue;

            Assert.NotEqual(from, to);
            AssertBelongsToFamily(from, family);
            AssertBelongsToFamily(to, family);
            Assert.True(
                migration.GetProperty("generationOrder").GetProperty("to").GetInt32()
                > migration.GetProperty("generationOrder").GetProperty("from").GetInt32());
            Assert.True(migration.GetProperty("ladderCompatible").GetBoolean());
            Assert.Equal(JsonValueKind.Object, evidence.ValueKind);
            Assert.Equal("noRegression", evidence.GetProperty("conclusion").GetString());

            var fromClass = migration.GetProperty("costClassFrom").GetString()!;
            var toClass = migration.GetProperty("costClassTo").GetString()!;
            Assert.True(costRank[toClass] <= costRank[fromClass]);
            Assert.Equal(fromClass, ActualCostClass(from));
            Assert.Equal(toClass, ActualCostClass(to));

            var evidenceText = File.ReadAllText(Path.Combine(RepositoryRoot(), evidence.GetProperty("reference").GetString()!));
            Assert.Contains($"\"model\": \"{from}\"", evidenceText);
            Assert.Contains($"\"model\": \"{to}\"", evidenceText);
        }
    }

    [Fact]
    public void TaskClassView_UsesOnlyCanonicalRoutesAndDeclaredFallbacks()
    {
        using var catalog = ReadJson("src/TokenEconomy/catalog/model-migrations.v1.json");
        using var policy = ReadJson("src/TokenEconomy/catalog/model-routing-policy.json");
        var routes = policy.RootElement.GetProperty("routes").EnumerateArray()
            .ToDictionary(route => route.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var fallback = Assert.Single(policy.RootElement.GetProperty("providerFallbacks").EnumerateArray());
        var fallbackFor = fallback.GetProperty("forRouteIds").EnumerateArray().Select(value => value.GetString()).ToHashSet(StringComparer.Ordinal);
        var expectedClasses = new[] { "bug", "chore", "dossier", "feature", "mechanical" };
        var actualClasses = new List<string>();

        foreach (var recommendation in catalog.RootElement.GetProperty("taskClassRecommendations").EnumerateArray())
        {
            actualClasses.Add(recommendation.GetProperty("taskClass").GetString()!);
            foreach (var candidate in recommendation.GetProperty("recommendedModelSet").EnumerateArray())
            {
                var route = routes[candidate.GetProperty("routeId").GetString()!];
                Assert.Equal(route.GetProperty("modelId").GetString(), candidate.GetProperty("model").GetString());
                Assert.Equal(route.GetProperty("thinkingLevel").GetString(), candidate.GetProperty("thinkingLevel").GetString());
            }

            var alternative = recommendation.GetProperty("quotaAwareAlternative");
            if (alternative.ValueKind == JsonValueKind.Null)
                continue;

            Assert.Equal(fallback.GetProperty("id").GetString(), alternative.GetProperty("routeId").GetString());
            Assert.Equal(fallback.GetProperty("modelId").GetString(), alternative.GetProperty("model").GetString());
            Assert.Equal(fallback.GetProperty("thinkingLevel").GetString(), alternative.GetProperty("thinkingLevel").GetString());
            foreach (var routeId in alternative.GetProperty("forRouteIds").EnumerateArray().Select(value => value.GetString()))
                Assert.Contains(routeId, fallbackFor);
        }

        Assert.Equal(expectedClasses, actualClasses.Order(StringComparer.Ordinal));
    }

    private static string ActualCostClass(string model) => ModelEfficiencyMatrix.Default.CostClassOf(model, AsOf) switch
    {
        CostClass.Economy => "economy",
        CostClass.Standard => "standard",
        CostClass.Premium => "premium",
        _ => "unknown",
    };

    private static void AssertBelongsToFamily(string model, string family)
    {
        var prefix = family switch
        {
            "claude-opus" => "claude-opus-",
            "claude-sonnet" => "claude-sonnet-",
            "claude-haiku" => "claude-haiku-",
            "gpt-mini" => "gpt-5.4-mini",
            "gpt" => "gpt-",
            _ => throw new InvalidOperationException($"Unknown family '{family}'."),
        };
        Assert.StartsWith(prefix, model, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJson(string relativePath)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath)));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
