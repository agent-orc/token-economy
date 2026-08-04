using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class AgentStudioTaskRoutingAdmissionTests
{
    private static readonly DateTime Start = new(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndToEndFixture_RoutesFallbacksPromotesIngestsAndReplaysDeterministically()
    {
        var card = new ComplexityCard
        {
            TaskKey = "TE-loop-fixture",
            Prompt = "Implement a reversible feature across two related components with an explicit integration test.",
            TaskType = "feature",
            AcceptanceCriteria = ["The integration path is deterministic."],
            ReferencedFiles = ["src/Launch.cs", "src/Storage.cs"],
            ReferencedSubsystems = ["launch", "storage"],
        };
        var estimates = new InMemoryTaskComplexityEstimateStore();
        var estimate = new TaskComplexityEstimator().Estimate(card);
        estimates.Upsert(estimate);
        Assert.Equal(TaskComplexityLevel.Standard, estimate.Level); // task in, policy features out

        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskRoutingAdmission(estimates, ledger);
        var importer = new AgentStudioTaskStorageImporter();

        var initial = admission.Admit(Request(card, 1, Snapshot(Start, AvailabilityWarningState.Healthy)));
        Assert.True(initial.MayLaunch);
        Assert.Equal("terra-medium", initial.LaunchRoute?.RouteId);
        Assert.Equal("gpt-5.6-luna", initial.Decision.ConfiguredModel);
        Assert.Equal("gpt-5.6-terra", initial.Decision.SelectedModel);
        Assert.False(initial.Decision.QuotaFallback);

        Ingest(importer, ledger, initial.Decision, "quota-truncation", Start.AddMinutes(5));
        var fallback = admission.Admit(Request(card, 2,
            Snapshot(Start.AddMinutes(6), AvailabilityWarningState.Warning, AvailabilityWarningState.Healthy),
            EvidenceForClaude()));
        Assert.True(fallback.MayLaunch);
        Assert.Equal(ModelRouteSelectionSource.EquivalentProviderFallback, fallback.Routing.SelectionSource);
        Assert.Equal("claude-sonnet-5", fallback.LaunchRoute?.ModelId);
        Assert.True(fallback.Decision.QuotaFallback);

        Ingest(importer, ledger, fallback.Decision, "semantic-failure", Start.AddMinutes(12));
        var promotedRequest = Request(card, 3, Snapshot(Start.AddMinutes(13), AvailabilityWarningState.Healthy));
        var promoted = admission.Admit(promotedRequest);
        Assert.True(promoted.MayLaunch);
        Assert.Equal("sol-medium", promoted.Routing.RecommendedRoute.RouteId);
        Assert.Contains("semanticReissuePromotion", promoted.Decision.AppliedHardFloorIds);
        Assert.Equal("gpt-5.6-sol", promoted.LaunchRoute?.ModelId);

        var replayBefore = promoted.Decision;
        var replay = admission.Admit(promotedRequest);
        Assert.Equal(JsonSerializer.Serialize(replayBefore), JsonSerializer.Serialize(replay.Decision));
        Assert.Equal(3, ledger.Decisions.Count);

        Ingest(importer, ledger, promoted.Decision, "successful", Start.AddMinutes(20));
        Ingest(importer, ledger, promoted.Decision, "successful", Start.AddMinutes(20));
        Assert.Equal(3, ledger.OutcomeObservations.Count);
        Assert.Equal(3, ledger.OutcomeClassifications.Count);
        Assert.Equal(AgentStudioAttemptOutcomeCategory.Successful,
            ledger.Records.Single(record => record.Run == 3).OutcomeCategory);

        var html = AgentStudioRoutingDecisionHtmlRenderer.Render(promoted.Decision);
        foreach (var label in new[] { "Recommended route", "Selected route", "Score", "Hard floor",
                     "Selection source", "Policy version", "Provisional status", "Quota fallback",
                     "Pin warning", "Wait reason", "Card configured route (unchanged)" })
            Assert.Contains(label, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Admission_NoSafeRoutePersistsWaitAndNeverReturnsLaunchRoute()
    {
        var card = new ComplexityCard
        {
            TaskKey = "TE-wait-fixture",
            Prompt = "Make a mechanical local copy change.",
            TaskType = "chore",
        };
        var estimates = new InMemoryTaskComplexityEstimateStore();
        estimates.Upsert(new TaskComplexityEstimator().Estimate(card));
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskRoutingAdmission(estimates, ledger);

        var result = admission.Admit(Request(card, 1, Snapshot(Start, AvailabilityWarningState.Critical)));

        Assert.False(result.MayLaunch);
        Assert.Null(result.LaunchRoute);
        Assert.Equal(ModelRoutingDisposition.Wait, result.Routing.Disposition);
        Assert.NotNull(result.Decision.WaitReason);
        Assert.Single(ledger.Decisions);
    }

    [Fact]
    public void Admission_ReissueUsesNewestClassifiedObservationForThePriorAttempt()
    {
        var card = new ComplexityCard
        {
            TaskKey = "TE-newest-evidence",
            Prompt = "Implement a reversible feature across two related components.",
            TaskType = "feature",
            ReferencedSubsystems = ["launch", "storage"],
        };
        var estimates = new InMemoryTaskComplexityEstimateStore();
        estimates.Upsert(new TaskComplexityEstimator().Estimate(card));
        var ledger = new InMemoryAgentStudioRunStore();
        var admission = new AgentStudioTaskRoutingAdmission(estimates, ledger);
        var importer = new AgentStudioTaskStorageImporter();
        var first = admission.Admit(Request(card, 1, Snapshot(Start, AvailabilityWarningState.Healthy)));
        Ingest(importer, ledger, first.Decision, "semantic-failure", Start.AddMinutes(2));
        Ingest(importer, ledger, first.Decision, "environmental-failure", Start.AddMinutes(3));

        var reissue = admission.Admit(Request(card, 2, Snapshot(Start.AddMinutes(4), AvailabilityWarningState.Healthy)));

        Assert.Equal(RoutingAttemptOutcome.EnvironmentalFailure, ledger.Records.Single().RoutingOutcome);
        Assert.Equal("terra-medium", reissue.Routing.RecommendedRoute.RouteId);
        Assert.DoesNotContain("semanticReissuePromotion", reissue.Decision.AppliedHardFloorIds);
    }

    private static AgentStudioTaskLaunchAdmissionRequest Request(
        ComplexityCard card,
        int run,
        ProviderAvailabilitySnapshot snapshot,
        RoutingEvidenceReport? evidence = null)
        => new()
        {
            Task = card,
            Run = run,
            ConfiguredModel = "gpt-5.6-luna",
            ConfiguredThinkingLevel = "medium",
            Capacity = new() { ProviderAvailability = snapshot, DeterministicVerificationAvailable = true },
            AvailableClis = snapshot.Providers.Any(row => row.Provider == "anthropic")
                ? [Cli.Codex, Cli.Claude] : [Cli.Codex],
            BenchmarkQualification = evidence,
            RequiredBenchmarkCapability = evidence is null ? null : "repository-editing",
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
            provider, cli, ProviderCliAvailability.Available, null, at.AddMinutes(-1), SnapshotFreshness.Fresh,
            warning, 0, 0, new(at, SnapshotCostStatus.Priced, []),
            [new("five-hour", null, at.AddHours(2), SnapshotFreshness.Fresh, warning,
                new(SnapshotValueOrigin.Observed, warning == AvailabilityWarningState.Critical ? 100 : 10, 100,
                    warning == AvailabilityWarningState.Critical ? 0 : 90,
                    warning == AvailabilityWarningState.Critical ? 100 : 10, at.AddMinutes(-1)), null)], []);

    private static void Ingest(
        AgentStudioTaskStorageImporter importer,
        InMemoryAgentStudioRunStore ledger,
        AgentStudioRoutingDecisionRecord decision,
        string outcome,
        DateTime observedAt)
    {
        var json = JsonSerializer.Serialize(new
        {
            taskKey = decision.TaskKey,
            taskType = "feature",
            capability = "repository-editing",
            run = decision.Run,
            routingDecision = decision,
            actualModel = decision.SelectedModel,
            actualThinkingLevel = decision.SelectedThinkingLevel,
            cliType = decision.SelectedModel == "claude-sonnet-5" ? "claude" : "codex",
            outcomeObservationId = $"{decision.TaskKey}:attempt:{decision.Run}:outcome:{outcome}",
            outcome,
            completedAt = observedAt.AddMinutes(-1),
            updatedAt = observedAt,
            tokenSummary = new { inputTokens = 100, outputTokens = 20 },
        });
        using var document = JsonDocument.Parse(json);
        ledger.Upsert(importer.Parse(document.RootElement));
    }

    private static RoutingEvidenceReport EvidenceForClaude()
    {
        var cohort = new RoutingEvidenceCohort
        {
            CanonicalModel = "claude-sonnet-5",
            ThinkingLevel = "high",
            TaskClass = "feature",
            Capability = "repository-editing",
            SampleSize = 5,
            AttemptLevelRouteCount = 5,
            CardLevelRouteCount = 0,
            UnknownRouteCount = 0,
            OutcomeAvailableCount = 5,
            SuccessCount = 5,
            SuccessRate = 1,
            OutcomeCoverage = 1,
            GradeAvailableCount = 5,
            FavorableGradeCount = 5,
            GradeCoverage = 1,
            FavorableGradeRate = 1,
            SemanticReissueAvailableCount = 5,
            SemanticReissueCount = 0,
            SemanticReissueCoverage = 1,
            SemanticReissueRate = 0,
            DurationAvailableCount = 5,
            DurationCoverage = 1,
            TotalDurationMs = 5,
            AverageDurationMs = 1,
            TokenAvailableCount = 5,
            TokenCoverage = 1,
            TotalTokens = 5,
            AverageTokens = 1,
            CostAvailableCount = 5,
            CostCoverage = 1,
            TotalCostUsd = 1,
            ObservedFrom = new(2026, 7, 1),
            ObservedThrough = new(2026, 7, 31),
            Provenance = [new() { ArtifactReference = "agent-studio-routing-loop-fixture" }],
            Qualification = new()
            {
                Level = RoutingQualificationLevel.BelowConfidenceGate,
                ClaimsValidation = false,
                GateFailures = ["fixture sample size is below the controlled confidence gate"],
            },
        };
        return new()
        {
            EvidenceVersion = "agent-studio-routing-loop-fixture-v1",
            ConfidenceGates = new(),
            ControlledCohorts = [],
            ObservationalCohorts = [cohort],
        };
    }
}
