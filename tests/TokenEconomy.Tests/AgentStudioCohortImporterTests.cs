using System.Text.Json.Nodes;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class AgentStudioCohortImporterTests
{
    private static string Fixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "data/agent-studio-cohorts/2026-09-18/run-records.json");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Dated cohort fixture missing.");
    }

    private static AgentStudioCohort Import(string? json = null) => new AgentStudioCohortImporter().Import(
        json ?? Fixture(), "run-records.json", TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

    private static CardEconomicsQuery Query(AgentStudioCohort cohort) => new()
    {
        OrganisationId = cohort.Organisation, TaskClass = "chore", AsOfUtc = cohort.ExportedAtUtc,
        Candidates = [new("claude-opus-5", "high"), new("gpt-5.6-sol", "xhigh"), new("gpt-6-astra", "low")],
        ExcludeObservedAccessRefusals = true,
    };

    [Fact]
    public void EveryExportedCostRepricesWithinRoundingWithoutDoubleCountingCachedInput()
    {
        var cohort = Import();
        Assert.Equal(20, cohort.Cards.Count);
        Assert.Equal(42, cohort.Records.Count);
        Assert.All(cohort.PriceChecks, check => Assert.True(check.MatchesWithinRounding, $"{check.TaskKey}/{check.Run}"));
        var astra = cohort.Records.Single(r => r.TaskKey == "AGT-2804");
        Assert.Equal(90064, astra.Usage.Input);
        Assert.Equal(1018624, astra.Usage.CacheRead);
        Assert.Equal(2.232464m, astra.CostEstimate);
        Assert.Equal(new DateTime(2026, 9, 18, 7, 51, 0, DateTimeKind.Utc), astra.ExecutedAtUtc);
        Assert.All(cohort.Records, r =>
        {
            Assert.Null(r.ActualThinkingLevel);
            Assert.Null(r.StartedAtUtc);
            Assert.Null(r.WeeklyQuota);
            Assert.Equal(AgentStudioReviewOutcome.Unknown, r.ReviewOutcome);
        });
        Assert.Equal(cohort.Sha256, Import().Sha256);
    }

    [Fact]
    public void RetainsAllRoundsButDoesNotTurnReviewPassesOrMergedLocallyIntoCompletion()
    {
        var cohort = Import();
        var summary = new AgentStudioCohortImporter().Summarize(cohort, Query(cohort).SinceUtc, cohort.ExportedAtUtc);
        var opus = summary.FullHistoryModels.Single(m => m.Model == "claude-opus-5");
        Assert.Equal(13, opus.RecordedRuns);
        Assert.Equal(5, opus.CompletedCards);
        Assert.Equal(169.9119935m, opus.TotalRunUsd);
        Assert.Equal(86.2939035m, opus.CompletedHistoryUsd);
        Assert.Equal(17.2587807m, opus.UsdPerCompletedCard);
        var day = summary.WindowModels.Single(m => m.Model == "claude-opus-5");
        Assert.Equal(3, day.CompletedCards);
        Assert.Equal(21.159029m, day.CompletedHistoryUsd);
        foreach (var model in summary.FullHistoryModels.Where(m => m.Model.StartsWith("gpt-", StringComparison.Ordinal)))
        {
            Assert.Equal(0, model.CompletedCards);
            Assert.Null(model.UsdPerCompletedCard);
        }
        Assert.All(cohort.Records.Where(r => r.TaskKey == "AGT-2826"), r => Assert.Equal(OutcomeQualitySignal.Unknown, r.CardOutcome));
        Assert.Contains(cohort.Cards, c => c.TaskKey == "AGT-2880");
        Assert.Contains("2026-09-18T16:23 Pass", cohort.Cards.Single(c => c.TaskKey == "AGT-2871").Reviews);
    }

    [Fact]
    public void UnknownLevelsCannotQualifyARecommendedLevelAndRefusalsStayOnAstra()
    {
        var cohort = Import();
        var decision = new CardEconomics().Decide(Query(cohort), cohort);
        Assert.StartsWith("No evidence-backed", decision.Recommendation);
        Assert.All(decision.Rows, r => Assert.Null(r.ExpectedUsdPerCompletedCard));
        var astra = decision.Rows.Single(r => r.Candidate.Model == "gpt-6-astra");
        Assert.True(astra.Selectable);
        Assert.True(astra.Provisional);
        Assert.False(astra.Eligible);
        Assert.Equal(5, astra.AvailabilityTotalRuns);
        Assert.Equal(0, astra.AvailabilityObservedRuns);
        var fact = Assert.Single(astra.OrganisationEvents);
        Assert.Equal(2, fact.EventCount);
        Assert.Null(fact.RunRate);
        Assert.Null(fact.LastSeenUtc);
        Assert.Equal(new[] { "AGT-2806", "AGT-2871" }, fact.TaskKeys);
        Assert.Empty(decision.Rows.Single(r => r.Candidate.Model == "gpt-5.6-sol").OrganisationEvents);
        var html = AgentStudioRoutingDecisionHtmlRenderer.RenderEconomics(decision);
        Assert.Contains("17.2588", html);
        Assert.Contains("2 refusal events", html);
        Assert.Contains("reasoning levels unknown", html);
        Assert.Contains("Unknown", html);
    }

    [Fact]
    public void QuotaProbesShowAccountDeltasNeverInventPerRunAttribution()
    {
        var cohort = Import();
        var evidence = new CardEconomics().Decide(Query(cohort) with { Currency = CardEconomicsCurrency.WeeklyQuotaPercent }, cohort).CohortEvidence!;
        Assert.Equal(new decimal?[] { 5, 8 }, evidence.QuotaIntervals.Select(i => i.CodexPercentagePoints));
        Assert.Equal(new decimal?[] { 4, 5 }, evidence.QuotaIntervals.Select(i => i.ClaudePercentagePoints));
        Assert.All(evidence.FullHistoryModels, m =>
        {
            Assert.Null(m.WeeklyQuotaPercentPerRun);
            Assert.Null(m.WeeklyQuotaPercentPerCompletedCard);
        });
    }

    [Fact]
    public void InvalidInputSubtractionIsRejectedAndPriceMismatchIsExposed()
    {
        var json = JsonNode.Parse(Fixture())!;
        json["cards"]![1]!["runs"]![0]!["uncachedIn"] = 1108688;
        Assert.Throws<InvalidDataException>(() => Import(json.ToJsonString()));
        json = JsonNode.Parse(Fixture())!;
        json["cards"]![1]!["runs"]![0]!["usdCorrected"] = 12.42;
        Assert.False(Import(json.ToJsonString()).PriceChecks.Single(c => c.TaskKey == "AGT-2804").MatchesWithinRounding);
    }

    [Fact]
    public void OffsetFreeTimestampWithSecondsUsesTheExplicitSourceTimezone()
    {
        var json = JsonNode.Parse(Fixture())!;
        json["cards"]![1]!["runs"]![0]!["ts"] = "2026-09-18T09:51:30";
        var sourceTimeZone = TimeZoneInfo.CreateCustomTimeZone("fixture-offset", TimeSpan.FromHours(5.5), "fixture-offset", "fixture-offset");

        var cohort = new AgentStudioCohortImporter().Import(json.ToJsonString(), "run-records.json", sourceTimeZone);
        var record = cohort.Records.Single(r => r.TaskKey == "AGT-2804");

        Assert.Equal(new DateTime(2026, 9, 18, 4, 21, 30, DateTimeKind.Utc), record.ExecutedAtUtc);
    }

    [Fact]
    public void TimestampWithExplicitOffsetDoesNotUseTheSourceTimezone()
    {
        var json = JsonNode.Parse(Fixture())!;
        json["cards"]![1]!["runs"]![0]!["ts"] = "2026-09-18T09:51:30+04:00";

        var record = Import(json.ToJsonString()).Records.Single(r => r.TaskKey == "AGT-2804");

        Assert.Equal(new DateTime(2026, 9, 18, 5, 51, 30, DateTimeKind.Utc), record.ExecutedAtUtc);
    }

    [Fact]
    public void EvidenceCannotLeakAcrossOrganisationOrBeforeObservation()
    {
        var cohort = Import();
        Assert.Throws<ArgumentException>(() => new CardEconomics().Decide(Query(cohort) with { OrganisationId = "other" }, cohort));
        Assert.Throws<ArgumentException>(() => new CardEconomics().Decide(Query(cohort) with { AsOfUtc = cohort.ExportedAtUtc.AddSeconds(-1) }, cohort));
    }
}
