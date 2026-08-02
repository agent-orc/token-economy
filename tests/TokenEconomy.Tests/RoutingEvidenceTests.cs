using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class RoutingEvidenceTests
{
    private static readonly DateTime Observed = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Controlled_aliases_are_canonical_and_validation_requires_declared_gates()
    {
        var aggregator = new RoutingEvidenceAggregator(Catalog());
        var below = aggregator.Aggregate([Benchmark(19)], [], []);
        var qualified = aggregator.Aggregate([Benchmark(20)], [], []);

        var belowCohort = Assert.Single(below.ControlledCohorts);
        Assert.Equal("model-a", belowCohort.CanonicalModel);
        Assert.Equal(RoutingQualificationLevel.BelowConfidenceGate, belowCohort.Qualification.Level);
        Assert.False(belowCohort.Qualification.ClaimsValidation);
        Assert.Contains(belowCohort.Qualification.GateFailures, value => value.Contains("sample size 19", StringComparison.Ordinal));

        var qualifiedCohort = Assert.Single(qualified.ControlledCohorts);
        Assert.Equal(20, qualifiedCohort.SampleSize);
        Assert.Equal(1m, qualifiedCohort.GradeCoverage);
        Assert.Equal(1m, qualifiedCohort.DurationCoverage);
        Assert.Equal(1m, qualifiedCohort.TokenCoverage);
        Assert.Equal(1m, qualifiedCohort.SemanticReissueCoverage);
        Assert.Equal(RoutingQualificationLevel.Validated, qualifiedCohort.Qualification.Level);
        Assert.True(qualifiedCohort.Qualification.ClaimsValidation);

        var belowTrust = Assert.Single(RoutingEvidenceTrust.FromReport(below, "results/routing-evidence.json"));
        var qualifiedTrust = Assert.Single(RoutingEvidenceTrust.FromReport(qualified, "results/routing-evidence.json"));
        Assert.Equal(EvidenceOutcome.Inconclusive, belowTrust.Outcome);
        Assert.Equal(EvidenceOutcome.Supports, qualifiedTrust.Outcome);
    }

    [Fact]
    public void Observational_history_never_claims_controlled_validation()
    {
        var records = Enumerable.Range(1, 20).Select(index => Record(
            $"TE-{index}", 1, "model-a-snapshot", "medium", true,
            grade: "A", semanticReissue: false, route: AgentStudioRouteGranularity.Attempt)).ToArray();

        var report = new RoutingEvidenceAggregator(Catalog()).Aggregate([], [], records);

        var cohort = Assert.Single(report.ObservationalCohorts);
        Assert.Equal(RoutingQualificationLevel.ObservationalSupport, cohort.Qualification.Level);
        Assert.False(cohort.Qualification.ClaimsValidation);
        Assert.Empty(report.ControlledCohorts);
    }

    [Fact]
    public void Confidence_gates_cannot_weaken_policy_floors()
    {
        var gates = new RoutingEvidenceConfidenceGates { MinimumSampleSize = 1 };

        var error = Assert.Throws<ArgumentException>(() =>
            new RoutingEvidenceAggregator(Catalog()).Aggregate([], [], [], gates));

        Assert.Contains("routing-policy floor", error.Message);
    }

    [Fact]
    public void Partial_and_ambiguous_evidence_remains_unknown_instead_of_zero()
    {
        var first = Record("TE-1", 1, "model-a", "medium", false, route: AgentStudioRouteGranularity.Attempt)
            with { ObservedAtUtc = DateTime.UnixEpoch, ProvenanceReference = null };
        var conflicting = first with { Model = "model-b", ObservedAtUtc = first.ObservedAtUtc };

        var report = new RoutingEvidenceAggregator(Catalog()).Aggregate([], [], [first, conflicting]);

        var cohort = Assert.Single(report.ObservationalCohorts);
        Assert.Null(cohort.CanonicalModel);
        Assert.Null(cohort.ThinkingLevel);
        Assert.Equal(1, cohort.SampleSize);
        Assert.Equal(0, cohort.TokenAvailableCount);
        Assert.Null(cohort.TotalTokens);
        Assert.Equal(0, cohort.DurationAvailableCount);
        Assert.Null(cohort.AverageDurationMs);
        Assert.Equal(0, cohort.CostAvailableCount);
        Assert.Null(cohort.TotalCostUsd);
        Assert.Null(cohort.ObservedThrough);
        Assert.Equal(RoutingQualificationLevel.Unknown, cohort.Qualification.Level);

        var coverage = ComplexityBacktester.MeasureCoverage([first, first]);
        Assert.Equal(1, coverage.AttemptCount);
        Assert.Equal(0m, coverage.TokenCoverage);
        Assert.Equal(0m, coverage.DurationCoverage);
        Assert.Equal(0m, coverage.SemanticReissueCoverage);
    }

    [Fact]
    public void Attempt_history_uses_mixed_routes_and_does_not_copy_card_route_into_unknown_attempt()
    {
        using var json = JsonDocument.Parse("""
            {
              "taskKey":"TE-9", "model":"model-b", "thinkingLevel":"high",
              "taskType":"feature", "capability":"code-repair",
              "attempts":[
                {"run":1,"route":{"model":"model-a-snapshot","thinkingLevel":"medium"},"grade":"C","semanticReissue":false,"startedAt":"2026-07-25T10:00:00Z","completedAt":"2026-07-25T10:01:00Z","tokenSummary":{"inputTokens":10}},
                {"run":2,"route":{"model":"model-b","thinkingLevel":"high"},"grade":"A","semanticReissue":true,"startedAt":"2026-07-25T11:00:00Z","completedAt":"2026-07-25T11:02:00Z","tokenSummary":{"inputTokens":20}},
                {"run":3,"grade":"B","completedAt":"2026-07-25T12:00:00Z"}
              ]
            }
            """);
        var records = new AgentStudioTaskStorageImporter(Catalog()).ParseRecords(json.RootElement);

        Assert.Equal(3, records.Count);
        Assert.Equal("model-a", records[0].Model);
        Assert.Equal(AgentStudioRouteGranularity.Attempt, records[0].RouteGranularity);
        Assert.Equal("model-b", records[1].Model);
        Assert.True(records[1].SemanticReissue);
        Assert.Null(records[2].Model);
        Assert.Null(records[2].ThinkingLevel);
        Assert.Equal(AgentStudioRouteGranularity.Unknown, records[2].RouteGranularity);

        var report = new RoutingEvidenceAggregator(Catalog()).Aggregate([], [], records);
        Assert.Equal(3, report.ObservationalCohorts.Sum(cohort => cohort.SampleSize));
        Assert.Equal(2, report.ObservationalCohorts.Sum(cohort => cohort.AttemptLevelRouteCount));
        Assert.Equal(1, report.ObservationalCohorts.Sum(cohort => cohort.UnknownRouteCount));
    }

    [Fact]
    public void Duplicate_inputs_and_repeated_writes_are_idempotent()
    {
        var evidence = Benchmark(2);
        var aggregator = new RoutingEvidenceAggregator(Catalog());
        var once = aggregator.Aggregate([evidence], [], []);
        var duplicated = aggregator.Aggregate([evidence, evidence], [], []);
        Assert.Equal(JsonSerializer.Serialize(once), JsonSerializer.Serialize(duplicated));

        var directory = Path.Combine(Path.GetTempPath(), "routing-evidence-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "routing-evidence.json");
        try
        {
            RoutingEvidencePipeline.WriteDerived(path, once);
            var first = File.ReadAllBytes(path);
            RoutingEvidencePipeline.WriteDerived(path, once);
            Assert.Equal(first, File.ReadAllBytes(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Pipeline_preserves_raw_inputs_and_retains_hashed_provenance()
    {
        var root = FindRepositoryRoot();
        var storage = Path.Combine(root, "benchmarks", "fixtures", "agent-studio-routing-history");
        var raw = Path.Combine(storage, "TE-26", "task.json");
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(raw));
        var directory = Path.Combine(Path.GetTempPath(), "routing-pipeline-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(directory, "routing-evidence.json");
        try
        {
            var pipeline = new RoutingEvidencePipeline();
            var first = pipeline.Run(root, storage, output);
            var firstBytes = File.ReadAllBytes(output);
            var second = pipeline.Run(root, storage, output);

            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(firstBytes, File.ReadAllBytes(output));
            Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(raw)));
            Assert.NotEmpty(first.ControlledCohorts);
            Assert.NotEmpty(first.ObservationalCohorts);
            Assert.All(first.ControlledCohorts.SelectMany(cohort => cohort.Provenance),
                provenance => Assert.Matches("^[0-9a-f]{64}$", provenance.ArtifactSha256!));
            Assert.All(first.ObservationalCohorts.SelectMany(cohort => cohort.Provenance),
                provenance => Assert.Matches("^[0-9a-f]{64}$", provenance.ArtifactSha256!));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static ModelPriceCatalog Catalog() => new(
    [
        new() { ModelId = "model-a", Aliases = ["model-a-snapshot"] },
        new() { ModelId = "model-b" },
    ]);

    private static ControlledBenchmarkEvidence Benchmark(int count)
    {
        var run = new BenchmarkRunResult
        {
            SchemaVersion = 1, SetupId = "repair", RunId = "run-1", StartedAtUtc = Observed.AddMinutes(-1),
            CompletedAtUtc = Observed, TaskClass = "feature", Capability = "code-repair",
            Cases = Enumerable.Range(1, count).Select(index => new BenchmarkCaseResult
            {
                VariantId = "a", Model = index % 2 == 0 ? "model-a" : "model-a-snapshot", ThinkingLevel = "medium",
                Repetition = index, Succeeded = true, InvocationExitCode = 0, EvaluationExitCode = 0,
                Usage = new(10, 2), CostUsd = .01m, DurationMs = 100,
            }).ToArray(),
        };
        return new(run, "benchmarks/results/repair/run-1.json", "abc123");
    }

    private static AgentStudioRunRecord Record(
        string key, int run, string? model, string? thinking, bool telemetry,
        string? grade = null, bool? semanticReissue = null,
        AgentStudioRouteGranularity route = AgentStudioRouteGranularity.Card) => new()
    {
        TaskKey = key, Run = run, Model = model, ThinkingLevel = thinking, RouteGranularity = route,
        TaskType = "feature", Capability = "code-repair", Usage = telemetry ? new(10, 2) : default,
        TokenUsageAvailable = telemetry, ExecutedAtUtc = Observed, StartedAtUtc = telemetry ? Observed.AddSeconds(-1) : null,
        ObservedAtUtc = Observed, CostEstimate = telemetry ? .01m : null,
        CostStatus = telemetry ? PriceStatus.Resolved : PriceStatus.UsageUnavailable,
        Outcome = OutcomeQualitySignal.Successful, Grade = grade, SemanticReissue = semanticReissue,
        ProvenanceReference = $"agent-studio/{key}/{run}.json",
    };

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Test repository root was not found.");
    }
}
