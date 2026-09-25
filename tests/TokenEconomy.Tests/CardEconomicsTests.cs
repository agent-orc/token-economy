using System.Text.Json;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class CardEconomicsTests
{
    private static readonly DateTime Day = new(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc);
    private static CardEconomicsQuery Query() => new()
    {
        OrganisationId = "org", TaskClass = "feature", AsOfUtc = Day.AddDays(1), SubscriptionId = "pro",
        Candidates = [new("astra", "low"), new("sol", "xhigh")],
    };

    private static AgentStudioRunRecord Run(string task, int run, bool success, string model = "gpt-6-astra", string level = "low", DateTime? startedAt = null)
    {
        var start = startedAt ?? Day.AddHours(1);
        var json = JsonSerializer.Serialize(new
        {
            taskKey = task, organisationId = "org", taskType = "feature", finalLane = success ? "done" : "failed",
            attempts = new[] { new { run, model, thinkingLevel = level, finalLane = success ? "done" : "failed",
                startedAt = start, completedAt = start.AddMinutes(1),
                tokenSummary = new { inputTokens = 1000, cacheReadTokens = 100000, cacheWriteTokens = 100, outputTokens = 1000 },
                reviewGrade = success ? "A" : "D", providerErrorClass = "none" } },
        });
        using var doc = JsonDocument.Parse(json);
        return new AgentStudioTaskStorageImporter().Parse(doc.RootElement) with
        {
            Outcome = success ? OutcomeQualitySignal.Successful : OutcomeQualitySignal.Unsuccessful,
            WeeklyQuota = new("pro", "weekly", Day, Day.AddDays(7), Day.AddHours(1), Day.AddHours(2), 10, 11, true),
        };
    }

    [Fact]
    public void CostsAllRoundsAndFailedCardsOncePerCompletion()
    {
        var runs = new[] { Run("a", 1, false), Run("a", 2, true), Run("b", 1, false) };
        var row = new CardEconomics().Decide(Query(), runs.Concat(runs)).Rows.Single(r => r.Candidate.Model == "gpt-6-astra");
        Assert.Equal(2, row.Cards);
        Assert.Equal(1, row.CompletedCards);
        Assert.Equal(.5m, row.CompletionProbability);
        Assert.Equal(3m, row.ExpectedRoundsPerCompletedCard);
        Assert.Equal(3 * ModelPriceCatalog.Default.ComputeCost("astra", runs[0].Usage, runs[0].ExecutedAtUtc).Total, row.ExpectedUsdPerCompletedCard);
        Assert.Equal(300000m, row.TokensPerCompletedCard!.CacheRead);
        Assert.Equal(180m, row.ExpectedDurationSeconds);
        Assert.Equal(3m, row.ExpectedWeeklyQuotaPercentPerCompletedCard);
        Assert.Equal("small observational cohort", row.Confidence);
    }

    [Fact]
    public void MissingTokensDoNotBecomeZeroAndQuotaIsIndependent()
    {
        var row = new CardEconomics().Decide(Query(), [Run("a", 1, true) with { TokenUsageAvailable = false }]).Rows.Single(r => r.Candidate.Model == "gpt-6-astra");
        Assert.Null(row.ExpectedUsdPerCompletedCard);
        Assert.Null(row.TokensPerCompletedCard);
        Assert.Equal(1m, row.ExpectedWeeklyQuotaPercentPerCompletedCard);
    }

    [Fact]
    public void NoCompletionOrCohortCannotProduceCheapRecommendation()
    {
        var result = new CardEconomics().Decide(Query(), [Run("a", 1, false)]);
        Assert.All(result.Rows, row => Assert.Null(row.ExpectedUsdPerCompletedCard));
        Assert.StartsWith("No evidence-backed", result.Recommendation);
    }

    [Fact]
    public void MixedRouteTruncatedOpenAndOtherOrganisationCardsAreNotAttributed()
    {
        var result = new CardEconomics().Decide(Query(),
        [Run("a", 1, false), Run("a", 2, true, "gpt-5.6-sol", "xhigh"), Run("b", 3, true),
            Run("c", 1, true) with { CardOutcome = OutcomeQualitySignal.Unknown },
            Run("d", 1, true) with { OrganisationId = "other" }]);
        Assert.All(result.Rows, row => Assert.Equal(0, row.Cards));
        Assert.Equal(3, result.Rows.Single(r => r.Candidate.Model == "gpt-6-astra").ExcludedCards);
    }

    [Fact]
    public void RefusalFactsAreOrganisationAndModelScopedAndCanExclude()
    {
        var runs = Enumerable.Range(1, 8).Select(i => Run(i.ToString(), 1, i > 2) with
        {
            ProviderErrorClass = i <= 2 ? "unsupported_parameter" : "none",
            ProviderErrorMessage = i <= 2 ? "access_programs.cyber is not enabled for this organization" : null,
        }).Append(Run("elsewhere", 1, false) with { OrganisationId = "other", ProviderErrorClass = "unsupported_parameter" });
        var row = new CardEconomics().Decide(Query() with { ExcludeObservedAccessRefusals = true }, runs).Rows.Single(r => r.Candidate.Model == "gpt-6-astra");
        Assert.False(row.Eligible);
        Assert.Equal(.25m, Assert.Single(row.Errors).Rate);
        Assert.Equal(8, row.AvailabilityObservedRuns);
        Assert.NotNull(row.ExpectedUsdPerCompletedCard);
    }

    [Fact]
    public void CorrectnessFloorCannotBeBoughtAway()
    {
        var q = Query() with
        {
            AsOfUtc = Day.AddDays(8),
            Candidates = [new("astra", "low"), new("sol", "xhigh"), new("gpt-6-sol", "xhigh")],
            Card = new() { TaskKey = "new", TaskType = "feature", Prompt = "Fix fencing for lease ownership" },
        };
        var result = new CardEconomics().Decide(q,
            [Run("a", 1, true), Run("b", 1, true, "gpt-5.6-sol", "xhigh"),
                Run("c", 1, true, "gpt-6-sol", "xhigh", Day.AddDays(7).AddHours(1))]);
        Assert.False(result.Rows.Single(r => r.Candidate.Model == "gpt-6-astra").Eligible);
        Assert.False(result.Rows.Single(r => r.Candidate.Model == "gpt-5.6-sol").Eligible);
        Assert.True(result.Rows.Single(r => r.Candidate.Model == "gpt-6-sol").Eligible);
        Assert.Contains("gpt-6-sol/xhigh", result.Recommendation);
    }

    [Fact]
    public void ResetConcurrentAndDifferentSubscriptionQuotaRemainUnknown()
    {
        var run = Run("a", 1, true);
        foreach (var probe in new[] { run.WeeklyQuota! with { ExclusiveAttribution = false },
            run.WeeklyQuota! with { AfterAtUtc = Day.AddDays(8) }, run.WeeklyQuota! with { AfterUsedPercent = 9 },
            run.WeeklyQuota! with { SubscriptionId = "max" } })
        {
            var row = new CardEconomics().Decide(Query(), [run with { WeeklyQuota = probe }]).Rows[0];
            Assert.Null(row.ExpectedWeeklyQuotaPercentPerCompletedCard);
        }
    }

    [Fact]
    public void ImportAndHtmlPreserveUnknownsAndEscapeProviderText()
    {
        var record = Run("a", 1, true) with { ProviderErrorClass = "error", ProviderErrorMessage = "<script>" };
        var result = new CardEconomics().Decide(Query(), [record]);
        var html = AgentStudioRoutingDecisionHtmlRenderer.RenderEconomics(result);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Unknown", html);
        Assert.Contains("provisional=True", html);
    }
    [Fact]
    public void CurrencyChangesRankingWithoutConvertingDollarsToQuota()
    {
        var astra = Run("a", 1, true);
        var sol = Run("b", 1, true, "gpt-5.6-sol", "xhigh") with
        { WeeklyQuota = astra.WeeklyQuota! with { AfterUsedPercent = 15 } };
        Assert.Equal("gpt-5.6-sol", new CardEconomics().Decide(Query(), [astra, sol]).Rows[0].Candidate.Model);
        Assert.Equal("gpt-6-astra", new CardEconomics().Decide(Query() with { Currency = CardEconomicsCurrency.WeeklyQuotaPercent }, [astra, sol]).Rows[0].Candidate.Model);
    }

    [Fact]
    public void CardScopeFiltersAndDateWindowPreventEvidenceLeakage()
    {
        var q = Query() with { Card = new() { TaskKey = "new", Prompt = "Add label", TaskType = "feature", Area = "ui", ExpectedChangedLines = 20 } };
        var matching = Run("a", 1, true) with { Area = "ui", ExpectedChangedLines = 30 };
        var result = new CardEconomics().Decide(q, [matching, matching with { TaskKey = "new" },
            matching with { TaskKey = "large", ExpectedChangedLines = 600 },
            matching with { TaskKey = "old", ExecutedAtUtc = Day.AddDays(-1) },
            matching with { TaskKey = "future", ExecutedAtUtc = Day.AddDays(2) }]);
        Assert.Equal(1, result.Rows[0].Cards);
    }

    [Fact]
    public void NativeQuotaSnapshotsRequireFreshExclusiveSameWindowEvidence()
    {
        JsonElement Snapshot(string phase, int used, int hour, bool stale = false) => JsonSerializer.SerializeToElement(new
        {
            phase, runId = "run-1", cliType = "codex", plan = "Pro", fetchedAt = Day.AddHours(hour),
            snapshotAgeSec = 0, ttlSeconds = 60, stale,
            windows = new[] { new { label = "Weekly", usedPct = used, resetAt = Day.AddDays(7) } },
        });
        var start = Snapshot("start", 10, 1); var end = Snapshot("end", 12, 2);
        Assert.Equal(2m, AgentStudioQuotaEvidence.FromSnapshots(start, end, "pro", true)!.Share);
        Assert.Null(AgentStudioQuotaEvidence.FromSnapshots(start, end, "pro", false));
        Assert.Null(AgentStudioQuotaEvidence.FromSnapshots(start, Snapshot("end", 12, 2, true), "pro", true));
        Assert.Null(AgentStudioQuotaEvidence.FromSnapshots(start, Snapshot("end", 9, 2), "pro", true));
        var json = JsonSerializer.SerializeToElement(new
        {
            taskKey = "native", organisationId = "org", taskType = "feature", model = "gpt-6-astra", thinkingLevel = "low",
            quotaStart = start, quotaEnd = end, subscriptionId = "pro", quotaExclusiveAttribution = true,
            providerError = new { code = "unsupported_parameter", message = "access_programs.cyber is not enabled" },
        });
        var run = new AgentStudioTaskStorageImporter().Parse(json);
        Assert.Equal(2m, run.WeeklyQuota!.Share);
        Assert.Equal("unsupported_parameter", run.ProviderErrorClass);
        Assert.Equal("org", run.OrganisationId);
    }

    [Fact]
    public void SuccessfulWorkerAwaitingCardReviewIsNotACompletedCard()
    {
        var run = Run("a", 1, true) with { CardOutcome = OutcomeQualitySignal.NeedsReview };
        var result = new CardEconomics().Decide(Query(), [run]);
        Assert.All(result.Rows, row => Assert.Equal(0, row.CompletedCards));
        Assert.StartsWith("No evidence-backed", result.Recommendation);
    }

    [Fact]
    public void PricesUseEachExecutionDateAndMissingTariffPoisonsAggregate()
    {
        var prices = new ModelPriceCatalog([new ModelListing
        {
            ModelId = "gpt-6-astra",
            History = [new() { InputPerMTok = 1, OutputPerMTok = 1, ValidFrom = Day, ValidTo = Day.AddHours(2).AddTicks(-1) },
                new() { InputPerMTok = 2, OutputPerMTok = 2, ValidFrom = Day.AddHours(2) }],
        }]);
        var first = Run("a", 1, false);
        var second = Run("a", 2, true) with { ExecutedAtUtc = Day.AddHours(3) };
        var row = new CardEconomics(prices).Decide(Query(), [first, second]).Rows[0];
        Assert.Equal(.3063m, row.ExpectedUsdPerCompletedCard);
        var missing = new CardEconomics(new ModelPriceCatalog([])).Decide(Query(), [first, second]);
        Assert.All(missing.Rows, r => Assert.Null(r.ExpectedUsdPerCompletedCard));
    }

}
