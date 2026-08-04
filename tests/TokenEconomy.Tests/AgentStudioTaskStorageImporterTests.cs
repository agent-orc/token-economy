using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class AgentStudioTaskStorageImporterTests
{
    [Fact]
    public void ImportDirectory_MapsMetricsAndIsIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"token-economy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "card-1"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "card-1", "task.json"), """
            { "taskKey":"TE-5", "run":2, "project":"Token-Economy", "model":"claude-sonnet-5",
              "thinkingLevel":"high", "cliType":"claude", "taskType":"feature", "finalLane":"Done",
              "completedAt":"2026-07-10T12:00:00Z", "tokenSummary": { "inputTokens":100000, "outputTokens":20000, "cacheReadTokens":5000 } }
            """);
            var store = new InMemoryAgentStudioRunStore();
            var importer = new AgentStudioTaskStorageImporter();
            var first = importer.ImportDirectory(directory, store);
            var second = importer.ImportDirectory(directory, store);
            var record = Assert.Single(store.Records);
            Assert.Equal(1, first.RecordsUpserted); Assert.Equal(1, second.RecordsUpserted);
            Assert.Equal("TE-5", record.TaskKey); Assert.Equal(2, record.Run);
            Assert.Equal("anthropic", record.Provider); Assert.Equal(100000, record.Usage.Input);
            Assert.True(record.TokenUsageAvailable);
            Assert.Equal(OutcomeQualitySignal.Successful, record.Outcome); Assert.NotNull(record.CostEstimate);
            Assert.Equal(ModelPrice.EstimatedListPricesCaveat, record.CostCaveat);
            Assert.True(record.IsEstimatedListPrice);
            var view = Assert.Single(ModelRunViews.ByModelOverTime(store.Records));
            Assert.Equal(1, view.Runs); Assert.Equal(1, view.SuccessfulRuns); Assert.Equal("Token-Economy", view.Project);
            Assert.Equal("claude", view.CliType);
            Assert.True(view.CostStatus.IsFullyPriced);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Parse_UsesLastUsageWhenNoSummaryAndRetainsUnknownPrice()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-7", "model":"unpriced-model", "lane":"Blocked", "updatedAt":"2026-07-10T12:00:00Z",
              "lastUsage": { "promptTokens":12, "completionTokens":3 } }
            """);
        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        Assert.Equal(12, record.Usage.Input); Assert.Equal(3, record.Usage.Output);
        Assert.Equal(PriceStatus.UnknownModel, record.CostStatus); Assert.Null(record.CostEstimate);
        Assert.Null(record.CostCaveat); Assert.False(record.IsEstimatedListPrice);
        Assert.Equal(OutcomeQualitySignal.NeedsReview, record.Outcome);
    }

    [Fact]
    public void Parse_UsesStableUnknownTimestampWhenTaskHasNoTimestamp()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-8", "model":"unpriced-model", "lastUsage": { "promptTokens":12 } }
            """);

        var importer = new AgentStudioTaskStorageImporter();
        var first = importer.Parse(json.RootElement);
        var second = importer.Parse(json.RootElement);

        Assert.Equal(DateTime.UnixEpoch, first.ObservedAtUtc);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Parse_MissingUsageIsUnavailableRatherThanMeasuredZero()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-9", "model":"claude-sonnet-5", "updatedAt":"2026-07-10T12:00:00Z" }
            """);

        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);

        Assert.False(record.TokenUsageAvailable);
        Assert.Equal(PriceStatus.UsageUnavailable, record.CostStatus);
        Assert.Null(record.CostEstimate);
    }

    [Fact]
    public void ModelRunViews_RetainCliAndUnresolvedCostCoverage()
    {
        var observedAt = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
        var records = new[]
        {
            ViewRecord("codex", "gpt-5.6-sol", PriceStatus.NoPriceForDate, observedAt),
            ViewRecord("codex", "gpt-5.6-sol", PriceStatus.UsageUnavailable, observedAt, run: 2),
            ViewRecord("other-cli", "gpt-5.6-sol", PriceStatus.NoPriceForDate, observedAt, run: 3),
        };

        var views = ModelRunViews.ByModelOverTime(records);

        Assert.Equal(2, views.Count);
        var codex = views.Single(view => view.CliType == "codex");
        Assert.Equal(2, codex.Runs);
        Assert.Null(codex.CostEstimate);
        Assert.Equal(1, codex.CostStatus.NoPriceForDateRuns);
        Assert.Equal(1, codex.CostStatus.UsageUnavailableRuns);
        Assert.False(codex.CostStatus.IsFullyPriced);
    }

    [Fact]
    public void ModelRunViews_RetainUnconfirmedCatalogCoverage()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-unconfirmed", "model":"claude-sonnet-4-5", "cliType":"claude",
              "completedAt":"2026-08-02T12:00:00Z", "tokenSummary": { "inputTokens":10 } }
            """);

        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        var view = Assert.Single(ModelRunViews.ByModelOverTime([record]));

        Assert.True(record.CostUnconfirmed);
        Assert.Equal(1, view.CostStatus.UnconfirmedRuns);
        Assert.False(view.CostStatus.IsFullyPriced);
    }

    [Fact]
    public void ImportDirectory_EmitsStructuredFailureEventForUnreadableTask()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"token-economy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "card-1"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "card-1", "task.json"), "not json");
            AgentStudioImportEvent? observed = null;
            var importer = new AgentStudioTaskStorageImporter();
            importer.EventOccurred += importEvent => observed = importEvent;

            Assert.ThrowsAny<System.Text.Json.JsonException>(() => importer.ImportDirectory(directory, new InMemoryAgentStudioRunStore()));

            Assert.NotNull(observed);
            Assert.Equal("agent_studio.task_storage.import_failed", observed!.Name);
            Assert.Equal("JsonReaderException", observed.Context["errorType"]);
            Assert.Equal(Path.Combine(directory, "card-1", "task.json"), observed.Context["path"]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Parse_RetainsExecutionDateUsedForHistoricalCost()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-8", "model":"claude-sonnet-5", "completedAt":"2026-08-31T23:59:59Z",
              "updatedAt":"2026-09-02T12:00:00Z", "tokenSummary": { "inputTokens":1000000 } }
            """);

        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);

        Assert.Equal(new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc), record.ExecutedAtUtc);
        Assert.Equal(new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc), record.ObservedAtUtc);
        Assert.Equal(2.00m, record.CostEstimate); // introductory rate at execution, not the later update's $3 rate
    }

    [Fact]
    public void Parse_MapsOnlyUpfrontExpectedScopeAndRoutingFeatures()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            {
              "id":"TE-27", "prompt":"Implement routing policy", "taskType":"feature",
              "expectedFiles":["src/Expected.cs"], "expectedSubsystems":["routing","storage"],
              "changedFiles":["src/Eventual.cs","src/Leak.cs"], "diffStats":{"changedLines":9999},
              "routingFeatures": {
                "correctnessRisk":24, "expectedScope":8, "contextDemand":14,
                "taskUncertainty":6, "quotaAndCostHeadroom":3, "expectedChangedLines":180,
                "hardFloorTriggers":["public-protocol","persistent_state_migration"]
              }
            }
            """);

        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        var card = Assert.Single(ComplexityHistory.FromRunRecords([record])).Card;

        Assert.Equal(["src/Expected.cs"], card.ReferencedFiles);
        Assert.DoesNotContain("src/Eventual.cs", card.ReferencedFiles);
        Assert.Equal(180, card.ExpectedChangedLines);
        Assert.Equal(24, card.RoutingSignals.CorrectnessRisk);
        Assert.Equal(8, card.RoutingSignals.ExpectedScope);
        Assert.Equal(14, card.RoutingSignals.ContextDemand);
        Assert.Equal(6, card.RoutingSignals.TaskUncertainty);
        Assert.Equal(3, card.RoutingSignals.QuotaAndCostHeadroom);
        Assert.Equal(
            [ComplexityHardFloorTrigger.PublicProtocol, ComplexityHardFloorTrigger.PersistentStateMigration],
            card.HardFloorTriggers);
    }

    [Fact]
    public void ParseRecords_JoinsDecisionActualRouteResourcesReviewAndReissueReason()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            {
              "taskKey":"TE-30", "project":"Token-Economy", "taskType":"feature",
              "attempts":[{
                "run":1,
                "routingDecision":{
                  "decisionId":"decision-30-1", "policyVersion":"2026-07-24",
                  "recommendedRoute":{"model":"gpt-5.6-terra","thinkingLevel":"medium"},
                  "selectedRoute":{"model":"claude-sonnet-5","thinkingLevel":"high"},
                  "selectionSource":"equivalentProviderFallback", "effectivePolicyScore":45
                },
                "actualModel":"claude-sonnet-5", "actualThinkingLevel":"high", "cliType":"claude",
                "startedAt":"2026-08-03T10:00:00Z", "completedAt":"2026-08-03T10:02:00Z",
                "updatedAt":"2026-08-03T10:03:00Z", "reviewGrade":"C",
                "reissueReason":"substantive-c-or-d-review",
                "tokenSummary":{"inputTokens":100,"outputTokens":20}
              }]
            }
            """);

        var record = Assert.Single(new AgentStudioTaskStorageImporter().ParseRecords(json.RootElement));

        Assert.Equal("decision-30-1", record.RoutingDecisionId);
        Assert.Equal("2026-07-24", record.RoutingPolicyVersion);
        Assert.Equal("gpt-5.6-terra", record.RoutingDecision!.RecommendedModel);
        Assert.Equal("claude-sonnet-5", record.ActualModel);
        Assert.Equal("high", record.ActualThinkingLevel);
        Assert.Equal(120, record.Usage.Input + record.Usage.Output);
        Assert.Equal(120_000, record.OutcomeObservation!.DurationMs);
        Assert.Equal(PriceStatus.Resolved, record.CostStatus);
        Assert.Equal(AgentStudioReviewOutcome.GradeC, record.ReviewOutcome);
        Assert.Equal(AgentStudioAttemptOutcomeCategory.SubstantiveReview, record.OutcomeCategory);
        Assert.Equal(RoutingAttemptOutcome.SubstantiveLowGrade, record.RoutingOutcome);
        Assert.True(record.SemanticReissue);
        Assert.Equal("substantive-c-or-d-review", record.ReissueReason);

        var view = Assert.Single(ModelRunViews.ByModelOverTime([record]));
        Assert.Equal("high", view.ThinkingLevel);
        Assert.Equal("2026-07-24", view.PolicyVersion);
        Assert.Equal(AgentStudioOutcomeClassification.CurrentVersion, view.OutcomeClassificationVersion);
        Assert.Equal(1, view.ReviewOutcomeAvailableRuns);
        Assert.Equal(1, view.OutcomeCategoryCounts[AgentStudioAttemptOutcomeCategory.SubstantiveReview]);
    }

    [Fact]
    public void Replay_AppendsRawObservationsVersionsClassificationAndNeverRewritesDecision()
    {
        var importer = new AgentStudioTaskStorageImporter();
        var store = new InMemoryAgentStudioRunStore();
        using var firstJson = System.Text.Json.JsonDocument.Parse(ReplayJson(
            "observation-1", "successful", "", "2026-08-03T10:01:00Z"));
        using var secondJson = System.Text.Json.JsonDocument.Parse(ReplayJson(
            "observation-2", "semantic-failure", "semantic-failure", "2026-08-03T10:02:00Z"));
        var first = importer.Parse(firstJson.RootElement);
        var second = importer.Parse(secondJson.RootElement);

        store.Upsert(first);
        var beforeReplay = new RoutingEvidenceAggregator().Aggregate([], [], store.Records);
        Assert.Equal(0, Assert.Single(beforeReplay.ObservationalCohorts).SemanticReissueCount);
        var historicalDecision = Assert.Single(store.Decisions);
        store.Upsert(first); // exact replay is idempotent
        store.Upsert(second);
        store.Upsert(second); // updated snapshot replay is also idempotent

        Assert.Single(store.Decisions);
        Assert.Same(historicalDecision, Assert.Single(store.Decisions));
        Assert.Equal("gpt-5.6-terra", historicalDecision.SelectedModel);
        Assert.Equal(2, store.OutcomeObservations.Count);
        Assert.Equal(2, store.OutcomeClassifications.Count);
        Assert.Single(store.Records);
        Assert.Equal(AgentStudioAttemptOutcomeCategory.SemanticFailure, Assert.Single(store.Records).OutcomeCategory);

        var evidence = new RoutingEvidenceAggregator().Aggregate([], [], store.Records);
        var cohort = Assert.Single(evidence.ObservationalCohorts);
        Assert.Equal(1, cohort.SemanticReissueCount);
        Assert.Equal(1, cohort.DecisionJoinAvailableCount);
        Assert.Equal([AgentStudioOutcomeClassification.CurrentVersion], cohort.OutcomeClassificationVersions);
        Assert.Equal(AgentStudioOutcomeClassification.CurrentVersion, evidence.OutcomeClassificationVersion);
        Assert.Equal("decision-replay", historicalDecision.DecisionId);

        store.Upsert(second with
        {
            OutcomeClassification = second.OutcomeClassification! with
            {
                Version = AgentStudioOutcomeClassification.CurrentVersion + 1,
                Category = AgentStudioAttemptOutcomeCategory.EnvironmentalFailure,
            },
        });
        Assert.Equal(2, store.OutcomeObservations.Count);
        Assert.Equal(3, store.OutcomeClassifications.Count);

        var rewrittenDecision = second with
        {
            RoutingDecision = second.RoutingDecision! with { SelectedModel = "gpt-5.6-sol" },
        };
        Assert.Throws<ArgumentException>(() => store.Upsert(rewrittenDecision));
        Assert.Equal("gpt-5.6-terra", Assert.Single(store.Decisions).SelectedModel);
    }

    [Fact]
    public void InfrastructureOutcomesDoNotBecomeSemanticHistory()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            {
              "taskKey":"TE-infra", "prompt":"Implement a feature", "taskType":"feature",
              "attempts":[
                {"run":1,"actualModel":"gpt-5.6-terra","outcome":"successful","completedAt":"2026-08-03T10:01:00Z"},
                {"run":2,"actualModel":"gpt-5.6-terra","outcome":"broken-test-host","reissueReason":"broken-test-host","completedAt":"2026-08-03T10:02:00Z"}
              ]
            }
            """);
        var records = new AgentStudioTaskStorageImporter().ParseRecords(json.RootElement);

        Assert.Equal(AgentStudioAttemptOutcomeCategory.BrokenTestHost, records[1].OutcomeCategory);
        Assert.Equal(RoutingAttemptOutcome.BrokenTestHost, records[1].RoutingOutcome);
        Assert.False(records[1].SemanticReissue);
        Assert.Equal(0, Assert.Single(ComplexityHistory.FromRunRecords(records)).ReissueCount);

        var cohort = Assert.Single(new RoutingEvidenceAggregator().Aggregate([], [], records).ObservationalCohorts);
        Assert.Equal(1, cohort.OutcomeAvailableCount); // infrastructure is not model-quality evidence
        Assert.Equal(1, cohort.OutcomeCategoryCounts[AgentStudioAttemptOutcomeCategory.BrokenTestHost]);
    }

    private static string ReplayJson(string observationId, string outcome, string reason, string updatedAt) => $$$"""
        {
          "taskKey":"TE-replay", "taskType":"feature", "capability":"code-repair",
          "run":1, "actualModel":"gpt-5.6-terra", "actualThinkingLevel":"medium",
          "routingDecision":{"decisionId":"decision-replay","policyVersion":"2026-07-24",
            "selectedRoute":{"model":"gpt-5.6-terra","thinkingLevel":"medium"}},
          "outcomeObservationId":"{{{observationId}}}", "outcome":"{{{outcome}}}", "reissueReason":"{{{reason}}}",
          "completedAt":"2026-08-03T10:01:00Z", "updatedAt":"{{{updatedAt}}}",
          "tokenSummary":{"inputTokens":10,"outputTokens":2}
        }
        """;

    private static AgentStudioRunRecord ViewRecord(string cli, string model, PriceStatus status, DateTime observedAt, int run = 1) => new()
    {
        TaskKey = "TE-view", Run = run, Provider = "openai", CliType = cli, Model = model,
        Usage = new(10, 0), TokenUsageAvailable = status != PriceStatus.UsageUnavailable,
        ExecutedAtUtc = observedAt, ObservedAtUtc = observedAt, CostStatus = status,
        CostEstimate = null, Outcome = OutcomeQualitySignal.Unknown,
    };
}
