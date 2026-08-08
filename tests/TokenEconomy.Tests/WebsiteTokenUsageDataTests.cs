using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

/// <summary>
/// Guards the deployable token-usage projection (<c>website/data/token-usage.json</c>, written by
/// <c>scripts/generate-website-data.py</c>) against the evidence and the real cost API. The site is
/// static, so its charts read a generated file rather than this library; these tests are the seam
/// that keeps the generator's own price arithmetic from drifting away from
/// <see cref="ModelPriceCatalog.ComputeCost"/>, and keep the published aggregates tied to the
/// append-only run they claim to summarize.
/// </summary>
public class WebsiteTokenUsageDataTests
{
    private const int MoneyDecimals = 6;

    [Fact]
    public void Published_model_usage_matches_the_raw_capability_run()
    {
        var usage = LoadUsageData();
        var byModel = usage.GetProperty("byModel");
        var source = byModel.GetProperty("source");
        var run = LoadJson(Path.Combine(FindRepositoryRoot(), source.GetProperty("evidencePath").GetString()!));

        Assert.Equal(run.GetProperty("runId").GetString(), source.GetProperty("runId").GetString());
        var cases = run.GetProperty("cases").EnumerateArray()
            .Where(item => ModelPriceCatalog.Default.Find(item.GetProperty("model").GetString()) is not null)
            .ToArray();
        Assert.Equal(cases.Length, source.GetProperty("cases").GetInt32());

        var models = byModel.GetProperty("models").EnumerateArray().ToArray();
        Assert.Equal(
            run.GetProperty("models").EnumerateArray()
                .Count(item => ModelPriceCatalog.Default.Find(item.GetString()) is not null),
            models.Length);

        foreach (var model in models)
        {
            var id = model.GetProperty("model").GetString()!;
            Assert.NotNull(ModelPriceCatalog.Default.Find(id));
            var expected = cases.Where(item => item.GetProperty("model").GetString() == id).ToArray();
            Assert.Equal(expected.Length, model.GetProperty("cases").GetInt32());
            Assert.Equal(
                expected.Count(item => item.GetProperty("succeeded").GetBoolean()),
                model.GetProperty("casesPassed").GetInt32());

            var published = ReadUsage(model.GetProperty("usage"));
            var summed = Sum(expected.Select(item => ReadUsage(item.GetProperty("usage"))));
            Assert.Equal(summed, published);
            Assert.Equal(
                published.Input + published.Output + published.CacheRead + published.CacheWrite,
                model.GetProperty("tokens").GetInt64());
        }

        // Tokens are only comparable across models where a run actually reported them; a launch
        // failure records nothing and must not read as a frugal model.
        Assert.Equal(
            cases.Count(item => Total(ReadUsage(item.GetProperty("usage"))) > 0),
            source.GetProperty("casesWithUsage").GetInt32());
        Assert.Equal(
            cases.Count(item => Total(ReadUsage(item.GetProperty("usage"))) == 0),
            source.GetProperty("casesWithoutUsage").GetInt32());
        Assert.Equal(
            cases.Where(item => !item.GetProperty("succeeded").GetBoolean())
                .Sum(item => Total(ReadUsage(item.GetProperty("usage")))),
            source.GetProperty("failedCaseTokens").GetInt64());
    }

    [Fact]
    public void Published_capability_rows_only_reference_cataloged_models()
    {
        var benchmarks = LoadJson(Path.Combine(FindRepositoryRoot(), "website", "data", "benchmarks.json"));

        foreach (var study in benchmarks.GetProperty("capabilityStudies").EnumerateArray())
            foreach (var capability in study.GetProperty("capabilities").EnumerateArray())
                Assert.NotNull(ModelPriceCatalog.Default.Find(capability.GetProperty("model").GetString()));
    }

    [Fact]
    public void Published_model_cost_matches_ComputeCost_at_the_run_timestamp()
    {
        var byModel = LoadUsageData().GetProperty("byModel");
        var pricedAt = byModel.GetProperty("source").GetProperty("pricedAtUtc").GetDateTime().ToUniversalTime();

        foreach (var model in byModel.GetProperty("models").EnumerateArray())
        {
            var id = model.GetProperty("model").GetString()!;
            var cost = model.GetProperty("cost");
            var breakdown = ModelPriceCatalog.Default.ComputeCost(id, ReadUsage(model.GetProperty("usage")), pricedAt);

            Assert.Equal(breakdown.Status.ToString(), cost.GetProperty("status").GetString());
            if (!breakdown.HasPrice)
            {
                // A missing price must stay explicitly unpriced on the site, never a silent $0.
                Assert.Equal(JsonValueKind.Null, cost.GetProperty("totalUsd").ValueKind);
                Assert.False(cost.TryGetProperty("components", out _), id);
                continue;
            }

            Assert.Equal(Round(breakdown.Total!.Value), cost.GetProperty("totalUsd").GetDecimal());
            Assert.Equal(breakdown.Unconfirmed, cost.GetProperty("unconfirmed").GetBoolean());
            Assert.Equal(breakdown.Currency, cost.GetProperty("currency").GetString());

            var components = cost.GetProperty("components");
            Assert.Equal(Round(breakdown.InputCost), components.GetProperty("input").GetDecimal());
            Assert.Equal(Round(breakdown.OutputCost), components.GetProperty("output").GetDecimal());
            Assert.Equal(Round(breakdown.CacheReadCost), components.GetProperty("cacheRead").GetDecimal());
            Assert.Equal(Round(breakdown.CacheWriteCost), components.GetProperty("cacheWrite").GetDecimal());
        }
    }

    [Fact]
    public void Published_document_classes_partition_the_same_run()
    {
        var usage = LoadUsageData();
        var types = usage.GetProperty("byDocumentType").GetProperty("documentTypes").EnumerateArray().ToArray();
        var expected = usage.GetProperty("byModel").GetProperty("source");

        Assert.Equal(expected.GetProperty("runId").GetString(),
            usage.GetProperty("byDocumentType").GetProperty("source").GetProperty("runId").GetString());
        Assert.Equal(expected.GetProperty("cases").GetInt32(), types.Sum(type => type.GetProperty("cases").GetInt32()));
        Assert.Equal(expected.GetProperty("tokens").GetInt64(), types.Sum(type => type.GetProperty("tokens").GetInt64()));
        Assert.Equal(
            ReadUsage(expected.GetProperty("usage")),
            Sum(types.Select(type => ReadUsage(type.GetProperty("usage")))));
    }

    [Fact]
    public void Published_card_aggregates_match_the_backtest_snapshot()
    {
        var card = LoadUsageData().GetProperty("byCard");
        var source = card.GetProperty("source");
        var backtest = LoadJson(Path.Combine(FindRepositoryRoot(), source.GetProperty("evidencePath").GetString()!));
        var rows = backtest.GetProperty("rows").EnumerateArray().ToArray();

        Assert.Equal(rows.Length, source.GetProperty("cards").GetInt32());
        Assert.Equal(rows.Sum(row => row.GetProperty("ActualTokens").GetInt64()), source.GetProperty("tokens").GetInt64());
        Assert.Equal(
            rows.Where(row => row.GetProperty("ActualReissues").GetInt32() > 0).Sum(row => row.GetProperty("ActualTokens").GetInt64()),
            source.GetProperty("reissuedTokens").GetInt64());

        var taskTypes = card.GetProperty("taskTypes").EnumerateArray().ToArray();
        Assert.Equal(rows.Length, taskTypes.Sum(type => type.GetProperty("cards").GetInt32()));
        foreach (var type in taskTypes)
        {
            var matching = rows.Where(row => row.GetProperty("TaskType").GetString() == type.GetProperty("taskType").GetString()).ToArray();
            Assert.Equal(matching.Length, type.GetProperty("cards").GetInt32());
            Assert.Equal(matching.Sum(row => row.GetProperty("ActualTokens").GetInt64()), type.GetProperty("tokens").GetInt64());
        }

        var buckets = card.GetProperty("reissueBuckets").EnumerateArray().ToArray();
        Assert.Equal(rows.Length, buckets.Sum(bucket => bucket.GetProperty("cards").GetInt32()));
        Assert.Equal(source.GetProperty("tokens").GetInt64(), buckets.Sum(bucket => bucket.GetProperty("tokens").GetInt64()));
    }

    [Fact]
    public void Published_session_turns_are_costed_at_their_own_timestamps()
    {
        var session = LoadUsageData().GetProperty("session");
        var model = session.GetProperty("source").GetProperty("model").GetString()!;
        var turns = session.GetProperty("turns").EnumerateArray().ToArray();
        Assert.NotEmpty(turns);

        decimal total = 0m;
        decimal cacheWrite = 0m;
        foreach (var turn in turns)
        {
            var usage = ReadUsage(turn.GetProperty("usage"));
            var at = turn.GetProperty("completedAtUtc").GetDateTime().ToUniversalTime();
            var breakdown = ModelPriceCatalog.Default.ComputeCost(model, usage, at);

            Assert.True(breakdown.HasPrice, $"turn {turn.GetProperty("turn").GetInt32()} must be priced");
            Assert.Equal(Round(breakdown.Total!.Value), turn.GetProperty("costUsd").GetDecimal());
            Assert.Equal(Total(usage), turn.GetProperty("tokens").GetInt64());
            total += Round(breakdown.Total.Value);
            cacheWrite += Round(breakdown.CacheWriteCost);
        }

        var totals = session.GetProperty("totals");
        Assert.Equal(total, totals.GetProperty("costUsd").GetDecimal());
        Assert.Equal(cacheWrite, totals.GetProperty("cacheWriteCostUsd").GetDecimal());
        Assert.Equal(
            ReadUsage(totals.GetProperty("usage")),
            Sum(turns.Select(turn => ReadUsage(turn.GetProperty("usage")))));
    }

    private static decimal Round(decimal value) => Math.Round(value, MoneyDecimals, MidpointRounding.ToEven);

    private static long Total(TokenUsage usage) => usage.Input + usage.Output + usage.CacheRead + usage.CacheWrite;

    private static TokenUsage ReadUsage(JsonElement usage) => new(
        usage.GetProperty("input").GetInt64(),
        usage.GetProperty("output").GetInt64(),
        usage.GetProperty("cacheRead").GetInt64(),
        usage.GetProperty("cacheWrite").GetInt64());

    private static TokenUsage Sum(IEnumerable<TokenUsage> items) => items.Aggregate(
        new TokenUsage(0, 0),
        (accumulated, item) => new TokenUsage(
            accumulated.Input + item.Input,
            accumulated.Output + item.Output,
            accumulated.CacheRead + item.CacheRead,
            accumulated.CacheWrite + item.CacheWrite));

    private static JsonElement LoadUsageData()
        => LoadJson(Path.Combine(FindRepositoryRoot(), "website", "data", "token-usage.json"));

    private static JsonElement LoadJson(string path)
        => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Test repository root was not found.");
    }
}
