namespace TokenEconomy;

/// <summary>How much operational trust is supported by evidence for a model.</summary>
/// <remarks>These values are derived from the ledger, not supplied by a vendor. <see cref="Restricted"/> wins while a material incident is open.</remarks>
public enum TrustLevel
{
    /// <summary>No independently verifiable successful evidence has been collected.</summary>
    Unverified = 0,
    /// <summary>Some successful evidence exists, but not enough to establish routine trust.</summary>
    Provisional = 1,
    /// <summary>Repeated independently verifiable successful evidence exists.</summary>
    Verified = 2,
    /// <summary>An open high-severity or critical incident prevents normal use.</summary>
    Restricted = 3,
}

/// <summary>Where a piece of capability evidence came from.</summary>
public enum EvidenceSource
{
    /// <summary>A statement by the vendor, model, or its operator. It never proves trust.</summary>
    SelfReportedClaim,
    /// <summary>A durable record from a normal, observed model run.</summary>
    ObservedRun,
    /// <summary>A repeatable controlled benchmark with a retained result artifact.</summary>
    ControlledBenchmark,
    /// <summary>An independently reviewed audit or external evaluation.</summary>
    ExternalAudit,
}

/// <summary>The result asserted by evidence for one capability.</summary>
public enum EvidenceOutcome
{
    /// <summary>The retained artifact supports the capability.</summary>
    Supports,
    /// <summary>The retained artifact contradicts the capability.</summary>
    DoesNotSupport,
    /// <summary>The retained artifact does not establish either outcome.</summary>
    Inconclusive,
}

/// <summary>Severity of an operational violation or incident.</summary>
public enum IncidentSeverity
{
    /// <summary>Minor incident with limited impact.</summary>
    Low,
    /// <summary>Material incident that merits review.</summary>
    Medium,
    /// <summary>Serious incident that restricts normal use while open.</summary>
    High,
    /// <summary>Most serious incident category; it restricts normal use while open.</summary>
    Critical,
}

/// <summary>Lifecycle state of an incident. Only open incidents affect current trust.</summary>
public enum IncidentStatus
{
    /// <summary>The incident is being investigated or has not been remediated.</summary>
    Open,
    /// <summary>The incident has been remediated and closed.</summary>
    Resolved,
    /// <summary>The report was determined not to be a valid incident.</summary>
    Rejected,
}

/// <summary>
/// A curator's capability assertion for a model. It intentionally carries no trust level: assertions
/// are inventory, while proof is represented by <see cref="TrustEvidence"/>.
/// </summary>
public sealed record ModelCapabilityRecord
{
    /// <summary>Canonical model id or the host's stable model identifier.</summary>
    public required string ModelId { get; init; }
    /// <summary>Specific capability being tracked, for example <c>repository-editing</c>.</summary>
    public required string Capability { get; init; }
    /// <summary>Who supplied the assertion (vendor, operator, or import source).</summary>
    public required string ClaimedBy { get; init; }
    /// <summary>When the assertion was recorded, in UTC.</summary>
    public required DateTime ClaimedAtUtc { get; init; }
    /// <summary>Optional scope or limitations. This is explanatory and never evidence.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// A durable reference to evidence about a model capability. A self-reported claim is retained for
/// traceability but does not count as proof; independent successful evidence needs a retained artifact.
/// </summary>
public sealed record TrustEvidence
{
    /// <summary>Stable, deduplicatable evidence identifier.</summary>
    public required string EvidenceId { get; init; }
    /// <summary>The model this evidence concerns.</summary>
    public required string ModelId { get; init; }
    /// <summary>The capability tested or observed.</summary>
    public required string Capability { get; init; }
    /// <summary>How the evidence was collected.</summary>
    public required EvidenceSource Source { get; init; }
    /// <summary>Whether the artifact supports the capability.</summary>
    public required EvidenceOutcome Outcome { get; init; }
    /// <summary>When the observation occurred, in UTC.</summary>
    public required DateTime ObservedAtUtc { get; init; }
    /// <summary>Resolvable durable location of the source result, log, report, or fixture.</summary>
    public required string ArtifactReference { get; init; }
    /// <summary>Optional content hash of the artifact, for tamper-evident evidence stores.</summary>
    public string? ArtifactSha256 { get; init; }
    /// <summary>Derived qualification report that applied confidence gates, when applicable.</summary>
    public string? QualificationReference { get; init; }

    /// <summary>True only for successful, independently collected evidence with a retained artifact.</summary>
    public bool IsIndependentProof => Outcome == EvidenceOutcome.Supports
        && Source != EvidenceSource.SelfReportedClaim
        && !string.IsNullOrWhiteSpace(ArtifactReference);
}

/// <summary>A recorded violation or incident that can constrain a model's trust level.</summary>
public sealed record ModelTrustIncident
{
    /// <summary>Stable, deduplicatable incident identifier.</summary>
    public required string IncidentId { get; init; }
    /// <summary>The model involved in the incident.</summary>
    public required string ModelId { get; init; }
    /// <summary>Short factual classification, for example <c>policy-bypass</c> or <c>data-leak</c>.</summary>
    public required string Kind { get; init; }
    /// <summary>Severity assigned by the incident process.</summary>
    public required IncidentSeverity Severity { get; init; }
    /// <summary>Current incident state.</summary>
    public required IncidentStatus Status { get; init; }
    /// <summary>When the incident occurred, in UTC.</summary>
    public required DateTime OccurredAtUtc { get; init; }
    /// <summary>CLI that launched the affected run, when the incident record identifies one.</summary>
    public Cli? Cli { get; init; }
    /// <summary>Run identifier, when the incident can be tied to one observed run.</summary>
    public string? RunId { get; init; }
    /// <summary>Durable incident report or supporting evidence location.</summary>
    public required string ArtifactReference { get; init; }
}

/// <summary>A counted model run used as the denominator for an operational violation rate.</summary>
/// <remarks>
/// A run is retained even when it provides no successful capability proof. This keeps the rate's
/// denominator separate from the evidence used to establish a trust level.
/// </remarks>
public sealed record ModelTrustRun
{
    /// <summary>Stable, deduplicatable run identifier.</summary>
    public required string RunId { get; init; }
    /// <summary>The model that performed the run.</summary>
    public required string ModelId { get; init; }
    /// <summary>CLI that launched the run, if recorded.</summary>
    public Cli? Cli { get; init; }
    /// <summary>When the run was observed, in UTC.</summary>
    public required DateTime ObservedAtUtc { get; init; }
    /// <summary>Durable run record used to count this denominator.</summary>
    public required string ArtifactReference { get; init; }
}

/// <summary>Evidence-backed operational violation rate for one model, optionally scoped to a CLI.</summary>
public sealed record ModelViolationRate
{
    /// <summary>The model evaluated.</summary>
    public required string ModelId { get; init; }
    /// <summary>Optional CLI scope. A null value means all CLIs.</summary>
    public Cli? Cli { get; init; }
    /// <summary>Counted observed runs. A zero denominator means the rate is not calculable.</summary>
    public required int ObservedRuns { get; init; }
    /// <summary>Non-rejected recorded violations in scope.</summary>
    public required int ViolationsObserved { get; init; }
    /// <summary>Violations divided by observed runs, or null when no run denominator is retained.</summary>
    public decimal? Rate { get; init; }
    /// <summary>Artifact references supporting the numerator and denominator.</summary>
    public required IReadOnlyList<string> Sources { get; init; }
    /// <summary>Plain-language caveat, including small-sample warnings.</summary>
    public required string Caveat { get; init; }
}

/// <summary>A transparent, derived trust result for one model.</summary>
public sealed record ModelTrustAssessment
{
    /// <summary>The model evaluated.</summary>
    public required string ModelId { get; init; }
    /// <summary>Derived trust level; never copied from a capability record.</summary>
    public required TrustLevel Level { get; init; }
    /// <summary>Number of declared capabilities in the ledger.</summary>
    public required int CapabilityClaimCount { get; init; }
    /// <summary>Number of distinct independently verifiable successful artifacts.</summary>
    public required int IndependentProofCount { get; init; }
    /// <summary>Number of counted observed runs for this model.</summary>
    public required int ObservedRunCount { get; init; }
    /// <summary>Number of non-rejected recorded violations for this model.</summary>
    public required int ViolationsObserved { get; init; }
    /// <summary>Violation rate across observed runs, or null when no denominator is retained.</summary>
    public decimal? ViolationRate { get; init; }
    /// <summary>Artifact references that support the model's trust and operational counts.</summary>
    public required IReadOnlyList<string> Sources { get; init; }
    /// <summary>Open incidents, newest first, which explain a restriction or downgrade.</summary>
    public required IReadOnlyList<ModelTrustIncident> OpenIncidents { get; init; }
    /// <summary>Short explanation suitable for an audit event or operator view.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Append-only in-memory evidence ledger and deterministic trust evaluator. It deliberately has no I/O:
/// hosts persist artifacts and supply durable references before recording an entry.
/// </summary>
public sealed class ModelTrustLedger
{
    /// <summary>Successful independent artifacts needed to move from provisional to verified.</summary>
    public const int VerifiedProofThreshold = 3;

    private readonly List<ModelCapabilityRecord> _capabilities = [];
    private readonly List<TrustEvidence> _evidence = [];
    private readonly List<ModelTrustIncident> _incidents = [];
    private readonly List<ModelTrustRun> _runs = [];
    private readonly HashSet<string> _entryIds = new(StringComparer.Ordinal);

    /// <summary>Records a capability assertion. Assertions never count as proof in <see cref="Assess"/>.</summary>
    public void RecordCapability(ModelCapabilityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Require(record.ModelId, nameof(record.ModelId));
        Require(record.Capability, nameof(record.Capability));
        Require(record.ClaimedBy, nameof(record.ClaimedBy));
        _capabilities.Add(record);
    }

    /// <summary>Appends uniquely identified evidence. Duplicate ids are rejected rather than overwritten.</summary>
    public void RecordEvidence(TrustEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Require(evidence.EvidenceId, nameof(evidence.EvidenceId));
        Require(evidence.ModelId, nameof(evidence.ModelId));
        Require(evidence.Capability, nameof(evidence.Capability));
        Require(evidence.ArtifactReference, nameof(evidence.ArtifactReference));
        AddId(evidence.EvidenceId);
        _evidence.Add(evidence);
    }

    /// <summary>Appends a uniquely identified incident. Incident records are never silently replaced.</summary>
    public void RecordIncident(ModelTrustIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        Require(incident.IncidentId, nameof(incident.IncidentId));
        Require(incident.ModelId, nameof(incident.ModelId));
        Require(incident.Kind, nameof(incident.Kind));
        Require(incident.ArtifactReference, nameof(incident.ArtifactReference));
        AddId(incident.IncidentId);
        _incidents.Add(incident);
    }

    /// <summary>Appends a uniquely identified observed run for transparent violation-rate denominators.</summary>
    public void RecordRun(ModelTrustRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Require(run.RunId, nameof(run.RunId));
        Require(run.ModelId, nameof(run.ModelId));
        Require(run.ArtifactReference, nameof(run.ArtifactReference));
        AddId(run.RunId);
        _runs.Add(run);
    }

    /// <summary>Derives the current trust level for a model from retained proof and open incidents.</summary>
    public ModelTrustAssessment Assess(string modelId)
    {
        Require(modelId, nameof(modelId));
        var claims = _capabilities.Count(record => SameModel(record.ModelId, modelId));
        var proofs = _evidence.Where(evidence => SameModel(evidence.ModelId, modelId) && evidence.IsIndependentProof)
            .Select(evidence => evidence.ArtifactReference).Distinct(StringComparer.Ordinal).Count();
        var open = _incidents.Where(incident => SameModel(incident.ModelId, modelId) && incident.Status == IncidentStatus.Open)
            .OrderByDescending(incident => incident.OccurredAtUtc).ThenBy(incident => incident.IncidentId, StringComparer.Ordinal).ToArray();

        var restricted = open.Any(incident => incident.Severity >= IncidentSeverity.High);
        var level = restricted ? TrustLevel.Restricted
            : proofs >= VerifiedProofThreshold ? TrustLevel.Verified
            : proofs > 0 ? TrustLevel.Provisional : TrustLevel.Unverified;
        var rationale = restricted
            ? $"{open.Count(incident => incident.Severity >= IncidentSeverity.High)} open material incident(s) restrict this model."
            : proofs == 0 ? "No independently verifiable successful evidence; capability claims are not proof."
            : proofs < VerifiedProofThreshold ? $"{proofs} independent proof(s); {VerifiedProofThreshold - proofs} more required for verified trust."
            : $"{proofs} independent successful proof(s) meet the verified-trust threshold.";

        var operationalRate = AssessViolationRate(modelId);
        var sources = _evidence.Where(evidence => SameModel(evidence.ModelId, modelId)).Select(evidence => evidence.ArtifactReference)
            .Concat(operationalRate.Sources).Distinct(StringComparer.Ordinal).OrderBy(reference => reference, StringComparer.Ordinal).ToArray();
        return new ModelTrustAssessment
        {
            ModelId = modelId, Level = level, CapabilityClaimCount = claims,
            IndependentProofCount = proofs, ObservedRunCount = operationalRate.ObservedRuns,
            ViolationsObserved = operationalRate.ViolationsObserved, ViolationRate = operationalRate.Rate,
            Sources = sources, OpenIncidents = open, Rationale = rationale,
        };
    }

    /// <summary>
    /// Calculates the observed violation rate for a model and, optionally, one CLI. A rate is never
    /// fabricated: it is null until a durable run denominator has been recorded.
    /// </summary>
    public ModelViolationRate AssessViolationRate(string modelId, Cli? cli = null)
    {
        Require(modelId, nameof(modelId));
        var runs = _runs.Where(run => SameModel(run.ModelId, modelId) && (cli is null || run.Cli == cli)).ToArray();
        var incidents = _incidents.Where(incident => SameModel(incident.ModelId, modelId)
            && incident.Status != IncidentStatus.Rejected && (cli is null || incident.Cli == cli)).ToArray();
        decimal? rate = runs.Length == 0 ? null : (decimal)incidents.Length / runs.Length;
        var caveat = runs.Length == 0
            ? "Rate unavailable: no retained observed-run denominator."
            : runs.Length < 30
                ? $"Small sample (N={runs.Length}); do not generalize this rate."
                : $"Observed rate from N={runs.Length} retained run(s).";
        var sources = runs.Select(run => run.ArtifactReference).Concat(incidents.Select(incident => incident.ArtifactReference))
            .Distinct(StringComparer.Ordinal).OrderBy(reference => reference, StringComparer.Ordinal).ToArray();

        return new ModelViolationRate
        {
            ModelId = modelId, Cli = cli, ObservedRuns = runs.Length, ViolationsObserved = incidents.Length,
            Rate = rate, Sources = sources, Caveat = caveat,
        };
    }

    private void AddId(string id)
    {
        if (!_entryIds.Add(id))
            throw new ArgumentException($"A trust-ledger entry with id '{id}' already exists.", nameof(id));
    }

    private static bool SameModel(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty value is required.", paramName);
    }
}

/// <summary>Converts gated routing cohorts into trust-ledger evidence without upgrading observations to validation.</summary>
public static class RoutingEvidenceTrust
{
    /// <summary>
    /// Creates one deterministic ledger entry per controlled cohort. Only a cohort whose retained
    /// qualification explicitly claims validation supports the capability; all others are inconclusive.
    /// Observational cohorts are intentionally excluded because association is not controlled proof.
    /// </summary>
    public static IReadOnlyList<TrustEvidence> FromReport(RoutingEvidenceReport report, string reportArtifactReference)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportArtifactReference);
        return report.ControlledCohorts
            .Where(cohort => cohort.CanonicalModel is not null && cohort.Capability is not null)
            .SelectMany(cohort => cohort.Provenance.Select(provenance =>
            {
                var key = string.Join("\n", report.EvidenceVersion, cohort.CanonicalModel, cohort.ThinkingLevel,
                    cohort.PolicyVersion, cohort.TaskClass, cohort.Capability,
                    cohort.ObservedThrough?.ToString("O"), provenance.ArtifactReference);
                var id = "routing-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
                return new TrustEvidence
                {
                    EvidenceId = id, ModelId = cohort.CanonicalModel!, Capability = cohort.Capability!,
                    Source = EvidenceSource.ControlledBenchmark,
                    Outcome = cohort.Qualification.ClaimsValidation ? EvidenceOutcome.Supports : EvidenceOutcome.Inconclusive,
                    ObservedAtUtc = cohort.ObservedThrough is { } date
                        ? date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : DateTime.UnixEpoch,
                    ArtifactReference = provenance.ArtifactReference, ArtifactSha256 = provenance.ArtifactSha256,
                    QualificationReference = reportArtifactReference,
                };
            }))
            .OrderBy(item => item.ModelId, StringComparer.Ordinal)
            .ThenBy(item => item.Capability, StringComparer.Ordinal)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Converts joined Agent Studio observations into append-only trust-run denominators. Outcome
    /// categories remain in routing evidence; infrastructure failures are counted as runs but are
    /// never converted into negative capability proof.
    /// </summary>
    public static IReadOnlyList<ModelTrustRun> RunsFromObservations(IEnumerable<AgentStudioRunRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records
            .Where(record => record.OutcomeObservation is not null
                && !string.IsNullOrWhiteSpace(record.ActualModel)
                && !string.IsNullOrWhiteSpace(record.ProvenanceReference))
            .GroupBy(record => record.OutcomeObservation!.ObservationId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(record => record.ObservedAtUtc).First())
            .Select(record => new ModelTrustRun
            {
                RunId = record.OutcomeObservation!.ObservationId,
                ModelId = record.ActualModel!,
                Cli = record.CliType?.Trim().ToLowerInvariant() switch
                {
                    "codex" => TokenEconomy.Cli.Codex,
                    "claude" or "claude-code" => TokenEconomy.Cli.Claude,
                    _ => null,
                },
                ObservedAtUtc = record.ObservedAtUtc,
                ArtifactReference = record.ProvenanceReference!,
            })
            .OrderBy(run => run.RunId, StringComparer.Ordinal)
            .ToArray();
    }
}
