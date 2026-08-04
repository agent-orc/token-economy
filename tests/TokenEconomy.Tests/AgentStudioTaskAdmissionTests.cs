using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class AgentStudioTaskAdmissionTests
{
    private static readonly DateTime FirstDecisionAt = new(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndToEnd_InitialRouteQuotaFallbackSemanticPromotionOutcomeImportAndReplayAreDeterministic()
    {
        var estimates = new InMemoryTaskComplexityEstimateStore();
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskAdmission(estimates, ledger);
        var configured = new OperatorModelRoutePin("gpt-5.6-luna", "medium");
        var initialRequest = Request(1, Snapshot(FirstDecisionAt, AvailabilityWarningState.Healthy), configured);

        var initial = admission.PrepareAttempt(initialRequest);

        Assert.True(initial.CanLaunch);
        Assert.Equal("terra-medium", initial.Routing.RecommendedRoute.RouteId);
        Assert.Equal("terra-medium", initial.Routing.SelectedRoute?.RouteId);
        Assert.Equal("gpt-5.6-terra", initial.LaunchRoute?.ModelId);
        Assert.Equal("gpt-5.6-luna", initial.PersistedDecision.ConfiguredModel);
        Assert.Equal("gpt-5.6-luna", initialRequest.CardConfiguredRoute!.ModelId); // no card rewrite
        Assert.Single(estimates.Estimates);
        Assert.Single(ledger.Decisions);

        ImportOutcome(ledger, initial.PersistedDecision, "semantic-failure", "semantic-failure",
            FirstDecisionAt.AddMinutes(5));
        Assert.Equal(AgentStudioAttemptOutcomeCategory.SemanticFailure, Assert.Single(ledger.Records).OutcomeCategory);

        var fallbackRequest = Request(2, Snapshot(FirstDecisionAt.AddMinutes(10),
            AvailabilityWarningState.Warning, AvailabilityWarningState.Healthy), configured) with
        {
            BenchmarkQualification = ClaudeFallbackEvidence(),
            RequiredBenchmarkCapability = "repository-editing",
        };
        var fallback = admission.PrepareAttempt(fallbackRequest);

        Assert.Equal("sol-medium", fallback.Routing.RecommendedRoute.RouteId);
        Assert.Contains("semanticReissuePromotion", fallback.Routing.CorrectnessFloor.AppliedFloorIds);
        Assert.Equal(ModelRouteSelectionSource.EquivalentProviderFallback, fallback.Routing.SelectionSource);
        Assert.Equal("claude-sonnet-5", fallback.LaunchRoute?.ModelId);
        Assert.True(fallback.PersistedDecision.QuotaFallbackApplied);
        Assert.True(fallback.PersistedDecision.SemanticPromotionApplied);
        Assert.Equal(FirstDecisionAt.AddMinutes(10), fallback.PersistedDecision.QuotaSnapshotDecisionAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(fallback.PersistedDecision.QuotaSnapshotId));
        Assert.Equal("gpt-5.6-luna", fallback.PersistedDecision.ConfiguredModel);

        ImportOutcome(ledger, fallback.PersistedDecision, "successful", null,
            FirstDecisionAt.AddMinutes(15));
        var observationsBeforeReplay = ledger.OutcomeObservations.Count;
        var classificationsBeforeReplay = ledger.OutcomeClassifications.Count;
        var replay = admission.PrepareAttempt(fallbackRequest);

        Assert.Equal(fallback.PersistedDecision.DecisionId, replay.PersistedDecision.DecisionId);
        Assert.Equal(fallback.PersistedDecision.QuotaSnapshotId, replay.PersistedDecision.QuotaSnapshotId);
        Assert.Equal(fallback.PersistedDecision.SelectedRouteId, replay.PersistedDecision.SelectedRouteId);
        Assert.Equal(fallback.PersistedDecision.AppliedHardFloorIds,
            replay.PersistedDecision.AppliedHardFloorIds);
        Assert.Equal(fallback.LaunchRoute, replay.LaunchRoute);
        Assert.Equal(2, ledger.Decisions.Count);
        Assert.Equal(observationsBeforeReplay, ledger.OutcomeObservations.Count);
        Assert.Equal(classificationsBeforeReplay, ledger.OutcomeClassifications.Count);
        Assert.Equal(AgentStudioAttemptOutcomeCategory.Successful,
            ledger.Records.Single(record => record.Run == 2).OutcomeCategory);
    }

    [Fact]
    public void PrepareAttempt_NoSafeRoutePersistsWaitAndRendererShowsEveryOperatorField()
    {
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskAdmission(new InMemoryTaskComplexityEstimateStore(), ledger);
        var request = Request(1, Snapshot(FirstDecisionAt, AvailabilityWarningState.Critical),
            new("gpt-5.6-luna", "medium"));

        var result = admission.PrepareAttempt(request);
        var html = AgentStudioRoutingDecisionHtmlRenderer.Render(result.PersistedDecision);

        Assert.False(result.CanLaunch);
        Assert.Equal(ModelRoutingDisposition.Wait, result.Routing.Disposition);
        Assert.Null(result.LaunchRoute);
        Assert.False(string.IsNullOrWhiteSpace(result.PersistedDecision.WaitOrOverrideReason));
        Assert.Contains("Recommended route", html);
        Assert.Contains("Selected route", html);
        Assert.Contains("Score", html);
        Assert.Contains("Hard floor", html);
        Assert.Contains("Selection source", html);
        Assert.Contains("Policy version", html);
        Assert.Contains("Recommended provisional", html);
        Assert.Contains("Quota fallback", html);
        Assert.Contains("Pin warning", html);
        Assert.Contains("Wait or override reason", html);
        Assert.Contains("gpt-5.6-terra", html);
        Assert.Contains("No route selected", html);
    }

    [Fact]
    public void PrepareAttempt_UsesNewestClassifiedEvidenceRatherThanAnOlderSemanticFailure()
    {
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskAdmission(new InMemoryTaskComplexityEstimateStore(), ledger);
        var configured = new OperatorModelRoutePin("gpt-5.6-luna", "medium");
        var first = admission.PrepareAttempt(Request(1,
            Snapshot(FirstDecisionAt, AvailabilityWarningState.Healthy), configured));
        ImportOutcome(ledger, first.PersistedDecision, "semantic-failure", "semantic-failure",
            FirstDecisionAt.AddMinutes(2));
        var second = admission.PrepareAttempt(Request(2,
            Snapshot(FirstDecisionAt.AddMinutes(3), AvailabilityWarningState.Healthy), configured));
        Assert.Equal("sol-medium", second.Routing.RecommendedRoute.RouteId);
        ImportOutcome(ledger, second.PersistedDecision, "broken-test-host", "broken-test-host",
            FirstDecisionAt.AddMinutes(5));

        var third = admission.PrepareAttempt(Request(3,
            Snapshot(FirstDecisionAt.AddMinutes(6), AvailabilityWarningState.Healthy), configured));

        Assert.Equal("terra-medium", third.Routing.RecommendedRoute.RouteId);
        Assert.DoesNotContain("semanticReissuePromotion", third.Routing.CorrectnessFloor.AppliedFloorIds);
        Assert.Equal(ModelRouteSelectionSource.PolicyRecommendation, third.Routing.SelectionSource);
    }

    [Fact]
    public void PrepareAttempt_ExplicitBelowFloorPinRemainsVisibleWithoutChangingCardConfiguration()
    {
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskAdmission(new InMemoryTaskComplexityEstimateStore(), ledger);
        var task = Card() with
        {
            HardFloorTriggers = [ComplexityHardFloorTrigger.SecurityBoundary],
            RoutingSignals = new()
            {
                CorrectnessRisk = 35, ExpectedScope = 20, ContextDemand = 20,
                TaskUncertainty = 10, QuotaAndCostHeadroom = 5,
            },
        };
        var configured = new OperatorModelRoutePin("gpt-5.6-luna", "medium");
        var result = admission.PrepareAttempt(new()
        {
            Task = task,
            Run = 1,
            Capacity = new() { ProviderAvailability = Snapshot(FirstDecisionAt, AvailabilityWarningState.Healthy) },
            AvailableClis = [Cli.Codex],
            OperatorPin = new("gpt-5.6-terra", "medium"),
            CardConfiguredRoute = configured,
        });

        Assert.True(result.CanLaunch); // policy permits an explicit pin, but makes the risk visible
        Assert.True(result.PersistedDecision.OperatorPinBelowPolicy);
        Assert.False(string.IsNullOrWhiteSpace(result.PersistedDecision.OperatorPinWarning));
        Assert.Equal("sol-xhigh", result.PersistedDecision.HardFloorRouteId);
        Assert.True(result.PersistedDecision.IsHardFloor);
        Assert.Equal(configured.ModelId, result.PersistedDecision.ConfiguredModel);
    }

    private static AgentStudioTaskAdmissionRequest Request(
        int run,
        ProviderAvailabilitySnapshot snapshot,
        OperatorModelRoutePin configured)
        => new()
        {
            Task = Card(),
            Run = run,
            Capacity = new() { ProviderAvailability = snapshot, DeterministicVerificationAvailable = true },
            AvailableClis = snapshot.Providers.Count == 1 ? [Cli.Codex] : [Cli.Codex, Cli.Claude],
            CardConfiguredRoute = configured,
        };

    private static ComplexityCard Card() => new()
    {
        TaskKey = "TE-admission-fixture",
        Prompt = "Add a reversible feature across two related components with deterministic tests.",
        TaskType = "feature",
        RoutingSignals = new()
        {
            CorrectnessRisk = 12,
            ExpectedScope = 8,
            ContextDemand = 8,
            TaskUncertainty = 6,
            QuotaAndCostHeadroom = 5,
        },
    };

    private static ProviderAvailabilitySnapshot Snapshot(
        DateTime at,
        AvailabilityWarningState openAi,
        AvailabilityWarningState? anthropic = null)
    {
        var rows = new List<ProviderAvailabilitySnapshotRow> { Provider("openai", "codex", at, openAi) };
        if (anthropic is { } state) rows.Add(Provider("anthropic", "claude", at, state));
        return new(at, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), rows);
    }

    private static ProviderAvailabilitySnapshotRow Provider(
        string provider,
        string cli,
        DateTime at,
        AvailabilityWarningState warning)
        => new(
            provider, cli, ProviderCliAvailability.Available, null, at.AddMinutes(-1),
            SnapshotFreshness.Fresh, warning, 0, 0, new(at, SnapshotCostStatus.Priced, []),
            [new("five-hour", at.AddHours(-1), at.AddHours(4), SnapshotFreshness.Fresh, warning,
                new(SnapshotValueOrigin.Observed, warning == AvailabilityWarningState.Critical ? 100 : 10,
                    100, warning == AvailabilityWarningState.Critical ? 0 : 90,
                    warning == AvailabilityWarningState.Critical ? 100 : 10, at.AddMinutes(-1)), null)], []);

    private static RoutingEvidenceReport ClaudeFallbackEvidence()
    {
        var cohort = new RoutingEvidenceCohort
        {
            CanonicalModel = "claude-sonnet-5", ThinkingLevel = "high", TaskClass = "feature",
            Capability = "repository-editing", SampleSize = 5, AttemptLevelRouteCount = 5,
            CardLevelRouteCount = 0, UnknownRouteCount = 0, OutcomeAvailableCount = 5,
            SuccessCount = 5, SuccessRate = 1, OutcomeCoverage = 1, GradeAvailableCount = 5,
            FavorableGradeCount = 5, GradeCoverage = 1, FavorableGradeRate = 1,
            SemanticReissueAvailableCount = 5, SemanticReissueCount = 0, SemanticReissueCoverage = 1,
            SemanticReissueRate = 0, DurationAvailableCount = 5, DurationCoverage = 1,
            TotalDurationMs = 5, AverageDurationMs = 1, TokenAvailableCount = 5, TokenCoverage = 1,
            TotalTokens = 5, AverageTokens = 1, CostAvailableCount = 5, CostCoverage = 1,
            TotalCostUsd = 1, ObservedFrom = new(2026, 7, 1), ObservedThrough = new(2026, 7, 31),
            Provenance = [new() { ArtifactReference = "fixture.json" }],
            Qualification = new()
            {
                Level = RoutingQualificationLevel.BelowConfidenceGate,
                ClaimsValidation = false,
                GateFailures = ["sample size below gate"],
            },
        };
        return new()
        {
            EvidenceVersion = "admission-fixture-v1", ConfidenceGates = new(),
            ControlledCohorts = [], ObservationalCohorts = [cohort],
        };
    }

    private static void ImportOutcome(
        InMemoryAgentStudioRunStore ledger,
        AgentStudioRoutingDecisionRecord decision,
        string outcome,
        string? reason,
        DateTime completedAt)
    {
        var payload = new
        {
            taskKey = decision.TaskKey,
            prompt = Card().Prompt,
            taskType = "feature",
            attempts = new[]
            {
                new
                {
                    run = decision.Run,
                    routingDecision = decision,
                    actualModel = decision.SelectedModel,
                    actualThinkingLevel = decision.SelectedThinkingLevel,
                    cliType = decision.SelectedModel?.StartsWith("claude", StringComparison.Ordinal) == true ? "claude" : "codex",
                    startedAt = completedAt.AddMinutes(-2),
                    completedAt,
                    updatedAt = completedAt,
                    outcome,
                    reissueReason = reason,
                    tokenSummary = new { inputTokens = 100, outputTokens = 20 },
                },
            },
        };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var imported = Assert.Single(new AgentStudioTaskStorageImporter().ParseRecords(json.RootElement));
        ledger.Upsert(imported);
        ledger.Upsert(imported); // exact task-storage replay must be idempotent
    }
}
