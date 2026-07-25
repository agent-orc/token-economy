using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelTrustLedgerTests
{
    private static readonly DateTime Now = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ClaimsAlone_NeverRaiseTrust()
    {
        var ledger = new ModelTrustLedger();
        ledger.RecordCapability(new() { ModelId = "model-a", Capability = "repository-editing", ClaimedBy = "vendor", ClaimedAtUtc = Now });
        ledger.RecordEvidence(new()
        {
            EvidenceId = "vendor-claim", ModelId = "model-a", Capability = "repository-editing",
            Source = EvidenceSource.SelfReportedClaim, Outcome = EvidenceOutcome.Supports,
            ObservedAtUtc = Now, ArtifactReference = "https://vendor.example/capabilities",
        });

        var assessment = ledger.Assess("MODEL-A");

        Assert.Equal(TrustLevel.Unverified, assessment.Level);
        Assert.Equal(1, assessment.CapabilityClaimCount);
        Assert.Equal(0, assessment.IndependentProofCount);
        Assert.Contains("not proof", assessment.Rationale);
    }

    [Fact]
    public void IndependentProofs_EstablishVerifiedTrust()
    {
        var ledger = new ModelTrustLedger();
        foreach (var id in new[] { "run-1", "bench-2", "audit-3" })
            ledger.RecordEvidence(new()
            {
                EvidenceId = id, ModelId = "model-a", Capability = "repository-editing",
                Source = EvidenceSource.ObservedRun, Outcome = EvidenceOutcome.Supports,
                ObservedAtUtc = Now, ArtifactReference = $"artifacts/{id}.json",
            });

        var assessment = ledger.Assess("model-a");

        Assert.Equal(TrustLevel.Verified, assessment.Level);
        Assert.Equal(ModelTrustLedger.VerifiedProofThreshold, assessment.IndependentProofCount);
    }

    [Fact]
    public void OpenMaterialIncident_RestrictsEvenAVerifiedModel()
    {
        var ledger = new ModelTrustLedger();
        foreach (var id in new[] { "run-1", "run-2", "run-3" })
            ledger.RecordEvidence(new()
            {
                EvidenceId = id, ModelId = "model-a", Capability = "repository-editing",
                Source = EvidenceSource.ObservedRun, Outcome = EvidenceOutcome.Supports,
                ObservedAtUtc = Now, ArtifactReference = $"artifacts/{id}.json",
            });
        ledger.RecordIncident(new()
        {
            IncidentId = "incident-1", ModelId = "model-a", Kind = "policy-bypass",
            Severity = IncidentSeverity.High, Status = IncidentStatus.Open,
            OccurredAtUtc = Now, ArtifactReference = "incidents/incident-1.json",
        });

        var assessment = ledger.Assess("model-a");

        Assert.Equal(TrustLevel.Restricted, assessment.Level);
        Assert.Single(assessment.OpenIncidents);
    }

    [Fact]
    public void DuplicateIds_AreRejectedInsteadOfOverwritingEvidence()
    {
        var ledger = new ModelTrustLedger();
        var entry = new TrustEvidence
        {
            EvidenceId = "proof-1", ModelId = "model-a", Capability = "repository-editing",
            Source = EvidenceSource.ObservedRun, Outcome = EvidenceOutcome.Supports,
            ObservedAtUtc = Now, ArtifactReference = "artifacts/proof-1.json",
        };
        ledger.RecordEvidence(entry);

        Assert.Throws<ArgumentException>(() => ledger.RecordEvidence(entry));
    }

    [Fact]
    public void ViolationRate_UsesRetainedRunsAndWarnsForSmallSamples()
    {
        var ledger = new ModelTrustLedger();
        ledger.RecordRun(new() { RunId = "run-1", ModelId = "model-a", Cli = Cli.Codex, ObservedAtUtc = Now, ArtifactReference = "runs/run-1.json" });
        ledger.RecordRun(new() { RunId = "run-2", ModelId = "model-a", Cli = Cli.Codex, ObservedAtUtc = Now, ArtifactReference = "runs/run-2.json" });
        ledger.RecordIncident(new()
        {
            IncidentId = "incident-1", ModelId = "model-a", Cli = Cli.Codex, Kind = "agent-git-violation",
            Severity = IncidentSeverity.Medium, Status = IncidentStatus.Resolved, OccurredAtUtc = Now,
            ArtifactReference = "incidents/incident-1.json",
        });

        var rate = ledger.AssessViolationRate("model-a", Cli.Codex);
        var assessment = ledger.Assess("model-a");

        Assert.Equal(2, rate.ObservedRuns);
        Assert.Equal(1, rate.ViolationsObserved);
        Assert.Equal(0.5m, rate.Rate);
        Assert.Contains("Small sample", rate.Caveat);
        Assert.Equal(rate.Rate, assessment.ViolationRate);
        Assert.Contains("runs/run-1.json", assessment.Sources);
    }

    [Fact]
    public void RateWithoutRunDenominator_IsUnavailableRatherThanZero()
    {
        var ledger = new ModelTrustLedger();
        HistoricalModelTrustEvidence.RecordInto(ledger);

        var rate = ledger.AssessViolationRate(HistoricalModelTrustEvidence.UnattributedModelId);

        Assert.Equal(3, rate.ViolationsObserved);
        Assert.Equal(0, rate.ObservedRuns);
        Assert.Null(rate.Rate);
        Assert.Contains("unavailable", rate.Caveat, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, HistoricalModelTrustEvidence.Incidents.Count);
    }
}
