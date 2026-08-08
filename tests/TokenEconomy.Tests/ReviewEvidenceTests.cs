using System.Security.Cryptography;
using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class ReviewEvidenceTests
{
    [Fact]
    public void Quality_studio_fixture_exercises_the_contract_without_becoming_model_evidence()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "benchmarks", "fixtures", "quality-studio-review-runs", "qs-review-fixture-001.json");
        var evidence = new QualityStudioReviewRunImporter().Import(path, "quality-studio-drop/qs-review-fixture-001.json");

        Assert.Equal("gpt-5.6-sol", evidence.CanonicalModel);
        Assert.Equal("medium", evidence.ThinkingLevel);
        Assert.Equal("codex", evidence.Cli);
        Assert.Equal("correctness", evidence.ReviewAspect);
        Assert.Equal(12, evidence.FilesReviewed);
        Assert.Equal(4, evidence.FindingsReported);
        Assert.Equal(3, evidence.ConfirmedFindings);
        Assert.Equal(1, evidence.DismissedFindings);
        Assert.Equal(PolicyEvidenceStatus.Observational, evidence.EvidenceStatus);
        Assert.True(evidence.IsFixture);
        Assert.False(evidence.EligibleForAggregation);
        Assert.Contains(evidence.EligibilityIssues, issue => issue.Contains("fixture", StringComparison.Ordinal));
    }

    [Fact]
    public void Pipeline_writes_append_only_runs_and_a_deterministic_versioned_report()
    {
        var root = RepositoryRoot();
        var fixture = Path.Combine(root, "benchmarks", "fixtures", "quality-studio-review-runs", "qs-review-fixture-001.json");
        var originalHash = SHA256.HashData(File.ReadAllBytes(fixture));
        var output = Path.Combine(Path.GetTempPath(), "review-evidence-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pipeline = new ReviewEvidencePipeline();
            var first = pipeline.Run(root, Path.GetDirectoryName(fixture)!, output);
            var reportPath = Path.Combine(output, "v1", "review-evidence.json");
            var runPath = Path.Combine(output, "v1", "runs", "qs-review-fixture-001.json");
            var reportBytes = File.ReadAllBytes(reportPath);
            var runBytes = File.ReadAllBytes(runPath);
            var second = pipeline.Run(root, Path.GetDirectoryName(fixture)!, output);

            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(reportBytes, File.ReadAllBytes(reportPath));
            Assert.Equal(runBytes, File.ReadAllBytes(runPath));
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(fixture)));
            Assert.Equal(1, first.ImportedRunCount);
            Assert.Equal(1, first.FixtureRunCount);
            Assert.Equal(0, first.EligibleOperationalRunCount);
            Assert.Empty(first.Cohorts);
            Assert.All(first.ModelSummaries, summary =>
            {
                Assert.Equal(ReviewEvidenceQuality.InsufficientEvidence, summary.EvidenceQuality);
                Assert.Equal(PolicyEvidenceStatus.Unknown, summary.EvidenceStatus);
                Assert.Null(summary.Suitability);
            });
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public void Append_only_run_rejects_changed_content_for_the_same_source_run_id()
    {
        var root = RepositoryRoot();
        var sourceFixture = Path.Combine(root, "benchmarks", "fixtures", "quality-studio-review-runs", "qs-review-fixture-001.json");
        var drop = Path.Combine(Path.GetTempPath(), "review-evidence-drop-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "review-evidence-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(drop);
        var copied = Path.Combine(drop, "run.json");
        try
        {
            File.Copy(sourceFixture, copied);
            var pipeline = new ReviewEvidencePipeline();
            pipeline.Run(root, drop, output);
            File.WriteAllText(copied, File.ReadAllText(copied).Replace("\"filesReviewed\": 12", "\"filesReviewed\": 13", StringComparison.Ordinal));

            var error = Assert.Throws<InvalidOperationException>(() => pipeline.Run(root, drop, output));
            Assert.Contains("Append-only review evidence", error.Message);
        }
        finally
        {
            if (Directory.Exists(drop)) Directory.Delete(drop, true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public void Operational_review_history_remains_observational_and_only_derives_fit_after_gates()
    {
        var sparse = new ReviewEvidenceAggregator().Aggregate([Run(1)]);
        var sparseSol = sparse.ModelSummaries.Single(summary => summary.CanonicalModel == "gpt-5.6-sol");
        Assert.Equal(PolicyEvidenceStatus.Observational, sparseSol.EvidenceStatus);
        Assert.Equal(ReviewEvidenceQuality.InsufficientEvidence, sparseSol.EvidenceQuality);
        Assert.Null(sparseSol.Suitability);

        var report = new ReviewEvidenceAggregator().Aggregate(Enumerable.Range(1, 20).Select(Run));
        var sol = report.ModelSummaries.Single(summary => summary.CanonicalModel == "gpt-5.6-sol");
        Assert.Equal(20, sol.RunCount);
        Assert.Equal(20, sol.ConfirmedFindings);
        Assert.Equal(0, sol.DismissedFindings);
        Assert.Equal(1m, sol.FindingOutcomeCoverage);
        Assert.Equal(1m, sol.FindingConfirmationRate);
        Assert.Equal(PolicyEvidenceStatus.Observational, sol.EvidenceStatus);
        Assert.Equal(ReviewEvidenceQuality.ObservationalSupport, sol.EvidenceQuality);
        Assert.Equal(Suitability.Ideal, sol.Suitability);
        Assert.Empty(sol.GateFailures);

        var knowledge = ModelRoutingKnowledgeBase.PolicyOnly.WithReviewEvidence(report);
        var matrix = ModelEfficiencyMatrix.FromKnowledge(knowledge);
        var suggestion = Assert.Single(matrix.SuggestModel(
            TaskClass.Review, BudgetPressure.Comfortable, [Cli.Codex],
            new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("gpt-5.6-sol", suggestion.ModelId);
        Assert.Equal(Suitability.Ideal, suggestion.Suitability);
        Assert.Equal(ReviewEvidenceQuality.ObservationalSupport, suggestion.ReviewQuality!.EvidenceQuality);
        Assert.Contains("observational Quality Studio runs", suggestion.Rationale);
        Assert.Contains("not controlled evidence", suggestion.Rationale);
    }

    [Fact]
    public void Review_evidence_gates_cannot_weaken_policy_coverage_or_sample_floors()
    {
        var aggregator = new ReviewEvidenceAggregator();
        Assert.Throws<ArgumentException>(() => aggregator.Aggregate([], new ReviewEvidenceConfidenceGates
        {
            MinimumRunCount = 19,
        }));
        Assert.Throws<ArgumentException>(() => aggregator.Aggregate([], new ReviewEvidenceConfidenceGates
        {
            MinimumFindingOutcomeCoverage = .69m,
        }));
    }

    [Fact]
    public void Pipeline_rejects_an_output_directory_inside_the_drop()
    {
        var drop = Path.Combine(RepositoryRoot(), "benchmarks", "fixtures", "quality-studio-review-runs");
        var error = Assert.Throws<ArgumentException>(() =>
            new ReviewEvidencePipeline().Run(RepositoryRoot(), drop, drop));
        Assert.Contains("outside the Quality Studio drop", error.Message);
    }

    [Fact]
    public void Committed_fixture_report_keeps_review_unknown_in_the_default_matrix()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        Assert.Equal("review-evidence-v1", knowledge.ReviewEvidenceVersion);
        Assert.Equal(knowledge.Models.Count, knowledge.ReviewQuality.Count);
        Assert.All(knowledge.ReviewQuality, summary =>
        {
            Assert.Equal(ReviewEvidenceQuality.InsufficientEvidence, summary.EvidenceQuality);
            Assert.Null(summary.Suitability);
        });

        Assert.Empty(ModelEfficiencyMatrix.Default.SuggestModel(
            TaskClass.Review, BudgetPressure.Comfortable, [Cli.Codex, Cli.Claude],
            new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc)));
        Assert.All(ModelEfficiencyMatrix.Default.Describe(new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc)),
            row => Assert.Null(row.Suitability[TaskClass.Review]));
    }

    [Fact]
    public void Schema_requires_the_quality_studio_route_scope_provenance_and_observational_classification()
    {
        var schemaPath = Path.Combine(RepositoryRoot(), "benchmarks", "schema", "quality-studio-review-run.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
        var root = document.RootElement;
        var required = root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();

        foreach (var property in new[] { "sourceRunId", "completedAtUtc", "model", "thinkingLevel", "cli", "reviewAspect", "scope", "evidenceStatus" })
            Assert.Contains(property, required);
        Assert.Equal("observational", root.GetProperty("properties").GetProperty("evidenceStatus").GetProperty("const").GetString());
    }

    private static QualityStudioReviewEvidence Run(int index) => new()
    {
        EvidenceVersion = "quality-studio-review-run-v1",
        SourceRunId = $"qs-real-{index:00}",
        ObservedAtUtc = new DateTimeOffset(2026, 8, 8, 12, index, 0, TimeSpan.Zero),
        SourceArtifactReference = $"quality-studio-drop/qs-real-{index:00}.json",
        SourceArtifactSha256 = new string('a', 64),
        Model = "sol",
        CanonicalModel = "gpt-5.6-sol",
        ThinkingLevel = "medium",
        Cli = "codex",
        ReviewAspect = "correctness",
        FilesReviewed = 2,
        FindingsReported = 1,
        ConfirmedFindings = 1,
        DismissedFindings = 0,
        EvidenceStatus = PolicyEvidenceStatus.Observational,
        IsFixture = false,
        EligibleForAggregation = true,
        EligibilityIssues = [],
    };

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
