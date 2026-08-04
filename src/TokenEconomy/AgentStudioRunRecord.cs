#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>The quality signal inferred from the final Agent Studio lane. It is intentionally a signal, not a claim of human evaluation.</summary>
public enum OutcomeQualitySignal { Unknown, Successful, NeedsReview, Unsuccessful }

/// <summary>How precisely the imported record identifies the model route for this attempt.</summary>
public enum AgentStudioRouteGranularity { Unknown, Card, Attempt }

/// <summary>
/// Durable outcome categories used to decide whether an attempt is model evidence or substrate
/// evidence. Only semantic failure and substantive review participate in semantic escalation.
/// </summary>
public enum AgentStudioAttemptOutcomeCategory
{
    Unknown,
    Successful,
    SemanticFailure,
    SubstantiveReview,
    EnvironmentalFailure,
    StaleBase,
    BrokenTestHost,
    Cancellation,
    QuotaTruncation,
    MissingDeliveryPath,
}

/// <summary>A normalized review result retained separately from the attempt's operational outcome.</summary>
public enum AgentStudioReviewOutcome
{
    Unknown,
    Approved,
    ChangesRequested,
    Rejected,
    GradeA,
    GradeB,
    GradeC,
    GradeD,
}

/// <summary>The immutable routing decision made before one Agent Studio attempt launched.</summary>
public sealed record AgentStudioRoutingDecisionRecord
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string DecisionId { get; init; }
    public required string TaskKey { get; init; }
    public required int Run { get; init; }
    /// <summary>The terminal admission disposition. A missing value identifies a legacy import.</summary>
    public ModelRoutingDisposition? Disposition { get; init; }
    public string? PolicyVersion { get; init; }
    public string? RecommendedRouteId { get; init; }
    public string? RecommendedModel { get; init; }
    public string? RecommendedThinkingLevel { get; init; }
    public bool? RecommendedRouteProvisional { get; init; }
    public string? SelectedRouteId { get; init; }
    public string? SelectedModel { get; init; }
    public string? SelectedThinkingLevel { get; init; }
    public bool? SelectedRouteProvisional { get; init; }
    public string? SelectionSource { get; init; }
    /// <summary>The effective policy score used for this attempt, including semantic evidence.</summary>
    public int? Score { get; init; }
    /// <summary>The immutable intake score retained separately from the effective attempt score.</summary>
    public int? UpfrontScore { get; init; }
    public string? HardFloorRouteId { get; init; }
    public string? HardFloorModel { get; init; }
    public string? HardFloorThinkingLevel { get; init; }
    public IReadOnlyList<string> AppliedHardFloorIds { get; init; } = [];
    public bool? IsHardFloor { get; init; }
    public bool? SemanticPromotionApplied { get; init; }
    /// <summary>The configured card route is audit context only; admission never rewrites it.</summary>
    public string? ConfiguredModel { get; init; }
    public string? ConfiguredThinkingLevel { get; init; }
    public string? OperatorPinModel { get; init; }
    public string? OperatorPinThinkingLevel { get; init; }
    public bool? OperatorPinBelowPolicy { get; init; }
    public string? OperatorPinWarning { get; init; }
    public bool? QuotaFallbackApplied { get; init; }
    public string? QuotaFallbackReason { get; init; }
    public string? WaitOrOverrideReason { get; init; }
    public DateTime? QuotaSnapshotDecisionAtUtc { get; init; }
    public string? QuotaSnapshotId { get; init; }
    public string? PolicyReason { get; init; }
    public string? Reason { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? ProvenanceReference { get; init; }
    public string? ProvenanceSha256 { get; init; }
}

/// <summary>
/// Append-only telemetry and raw outcome facts for one attempt observation. A changed source
/// snapshot produces another observation id; replaying the same snapshot produces the same id.
/// </summary>
public sealed record AgentStudioRunOutcomeObservation
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ObservationId { get; init; }
    public required string DecisionId { get; init; }
    public required string TaskKey { get; init; }
    public required int Run { get; init; }
    public string? ActualModel { get; init; }
    public string? ActualThinkingLevel { get; init; }
    public required TokenUsage Usage { get; init; }
    public bool TokenUsageAvailable { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public required DateTime ExecutedAtUtc { get; init; }
    public long? DurationMs { get; init; }
    public decimal? CostEstimate { get; init; }
    public required PriceStatus CostStatus { get; init; }
    public string? RawOutcome { get; init; }
    public string? RawReviewOutcome { get; init; }
    public string? Grade { get; init; }
    public bool? SemanticReissue { get; init; }
    public string? ReissueReason { get; init; }
    public required DateTime ObservedAtUtc { get; init; }
    public string? ProvenanceReference { get; init; }
    public string? ProvenanceSha256 { get; init; }
}

/// <summary>A versioned derivation over an immutable outcome observation.</summary>
public sealed record AgentStudioOutcomeClassification
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public required string ObservationId { get; init; }
    public required string DecisionId { get; init; }
    public required AgentStudioAttemptOutcomeCategory Category { get; init; }
    public required AgentStudioReviewOutcome ReviewOutcome { get; init; }
    public string? ReissueReason { get; init; }
    public bool IsSemanticFailure => Category is AgentStudioAttemptOutcomeCategory.SemanticFailure
        or AgentStudioAttemptOutcomeCategory.SubstantiveReview;
    public bool IsInfrastructureFailure => Category is AgentStudioAttemptOutcomeCategory.EnvironmentalFailure
        or AgentStudioAttemptOutcomeCategory.StaleBase
        or AgentStudioAttemptOutcomeCategory.BrokenTestHost
        or AgentStudioAttemptOutcomeCategory.MissingDeliveryPath;
    public RoutingAttemptOutcome RoutingOutcome => Category switch
    {
        AgentStudioAttemptOutcomeCategory.SemanticFailure => RoutingAttemptOutcome.SemanticFailure,
        AgentStudioAttemptOutcomeCategory.SubstantiveReview => RoutingAttemptOutcome.SubstantiveLowGrade,
        AgentStudioAttemptOutcomeCategory.EnvironmentalFailure => RoutingAttemptOutcome.EnvironmentalFailure,
        AgentStudioAttemptOutcomeCategory.StaleBase => RoutingAttemptOutcome.StaleBase,
        AgentStudioAttemptOutcomeCategory.BrokenTestHost => RoutingAttemptOutcome.BrokenTestHost,
        AgentStudioAttemptOutcomeCategory.Cancellation => RoutingAttemptOutcome.Cancellation,
        AgentStudioAttemptOutcomeCategory.QuotaTruncation => RoutingAttemptOutcome.QuotaTruncation,
        AgentStudioAttemptOutcomeCategory.MissingDeliveryPath => RoutingAttemptOutcome.MissingDeliveryPath,
        _ => RoutingAttemptOutcome.None,
    };
}

/// <summary>A single, deduplicatable model run imported from an Agent Studio task card.</summary>
public sealed record AgentStudioRunRecord
{
    /// <summary>Stable card key from task storage.</summary>
    public required string TaskKey { get; init; }
    /// <summary>Attempt/run number within the task. Together with <see cref="TaskKey"/> this is the idempotency key.</summary>
    public required int Run { get; init; }
    /// <summary>Immutable pre-launch decision joined to this attempt, when recorded.</summary>
    public AgentStudioRoutingDecisionRecord? RoutingDecision { get; init; }
    /// <summary>Append-only raw observation represented by this materialized attempt record.</summary>
    public AgentStudioRunOutcomeObservation? OutcomeObservation { get; init; }
    /// <summary>Versioned classification derived from <see cref="OutcomeObservation"/>.</summary>
    public AgentStudioOutcomeClassification? OutcomeClassification { get; init; }
    public string? Project { get; init; }
    public string? Provider { get; init; }
    /// <summary>The recorded model id. Null means task storage did not identify an unambiguous route.</summary>
    public string? Model { get; init; }
    public string? ThinkingLevel { get; init; }
    /// <summary>The model that actually ran, independent of the route recommendation.</summary>
    public string? ActualModel => OutcomeObservation?.ActualModel ?? Model;
    /// <summary>The thinking level that actually ran, independent of the route recommendation.</summary>
    public string? ActualThinkingLevel => OutcomeObservation?.ActualThinkingLevel ?? ThinkingLevel;
    public string? RoutingDecisionId => RoutingDecision?.DecisionId ?? OutcomeObservation?.DecisionId;
    public string? RoutingPolicyVersion => RoutingDecision?.PolicyVersion;
    public AgentStudioRouteGranularity RouteGranularity { get; init; }
    public string? CliType { get; init; }
    public string? TaskType { get; init; }
    public string? Capability { get; init; }
    /// <summary>Original pre-run card text retained for complexity calibration.</summary>
    public string? TaskPrompt { get; init; }
    public string? Area { get; init; }
    public string? EpicContext { get; init; }
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public IReadOnlyList<string> ReferencedFiles { get; init; } = [];
    public IReadOnlyList<string> ReferencedSubsystems { get; init; } = [];
    /// <summary>Scope expected on the card before launch; never sourced from a completed diff.</summary>
    public int? ExpectedChangedLines { get; init; }
    public int? DependencyFanOut { get; init; }
    public int? RepositoryFileCount { get; init; }
    public ComplexityRoutingSignals RoutingSignals { get; init; } = new();
    public IReadOnlyList<ComplexityHardFloorTrigger> HardFloorTriggers { get; init; } = [];
    public string? FinalLane { get; init; }
    public required TokenUsage Usage { get; init; }
    /// <summary>Distinguishes a measured zero-token attempt from absent token telemetry.</summary>
    public bool TokenUsageAvailable { get; init; }
    /// <summary>
    /// UTC instant used to resolve <see cref="CostEstimate"/> from the dated pricing catalog.
    /// This is normally the run completion timestamp; it remains distinct from
    /// <see cref="ObservedAtUtc"/> so a later card update cannot reprice a historical run.
    /// </summary>
    public required DateTime ExecutedAtUtc { get; init; }
    public decimal? CostEstimate { get; init; }
    public string? Currency { get; init; }
    public required PriceStatus CostStatus { get; init; }
    /// <summary>
    /// Consumer-facing qualification for <see cref="CostEstimate"/>. Catalog-derived
    /// estimates carry <see cref="ModelPrice.EstimatedListPricesCaveat"/> so a UI can
    /// render the list-price disclaimer alongside the number.
    /// </summary>
    public string? CostCaveat { get; init; }
    /// <summary>True when the dated catalog entry used for this run was explicitly unconfirmed.</summary>
    public bool CostUnconfirmed { get; init; }
    /// <summary>True when <see cref="CostEstimate"/> is based on published list prices rather than an invoice.</summary>
    public bool IsEstimatedListPrice => CostCaveat == ModelPrice.EstimatedListPricesCaveat;
    public required OutcomeQualitySignal Outcome { get; init; }
    /// <summary>Review grade when task storage records one; unrecognized values remain unknown.</summary>
    public string? Grade { get; init; }
    public AgentStudioReviewOutcome ReviewOutcome => OutcomeClassification?.ReviewOutcome ?? AgentStudioReviewOutcome.Unknown;
    public AgentStudioAttemptOutcomeCategory OutcomeCategory => OutcomeClassification?.Category ?? AgentStudioAttemptOutcomeCategory.Unknown;
    public string? ReissueReason => OutcomeClassification?.ReissueReason ?? OutcomeObservation?.ReissueReason;
    public RoutingAttemptOutcome RoutingOutcome => OutcomeClassification?.RoutingOutcome ?? RoutingAttemptOutcome.None;
    /// <summary>
    /// True only when storage explicitly identifies a semantic failure/reissue or substantive C/D
    /// review. A retry number alone is not enough because substrate and delivery failures are not semantic.
    /// </summary>
    public bool? SemanticReissue { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public required DateTime ObservedAtUtc { get; init; }
    /// <summary>Source task artifact, when imported from a directory.</summary>
    public string? ProvenanceReference { get; init; }
    /// <summary>SHA-256 of the source task artifact. The importer never rewrites that raw file.</summary>
    public string? ProvenanceSha256 { get; init; }
}

/// <summary>Converts enriched imported runs into estimator calibration samples.</summary>
public static class ComplexityHistory
{
    /// <remarks>Cards without an imported prompt cannot be used for upfront similarity and are omitted.</remarks>
    public static IReadOnlyList<ComplexityHistorySample> FromRunRecords(IEnumerable<AgentStudioRunRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records
            .GroupBy(record => record.TaskKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Any(record => !string.IsNullOrWhiteSpace(record.TaskPrompt)))
            .Select(group =>
            {
                // A store normally deduplicates task key + run. Keep this conversion safe for
                // callers passing raw imported records as well, otherwise retries inflate cost.
                var attempts = group
                    .GroupBy(record => record.Run)
                    .Select(run => run.OrderByDescending(record => record.ObservedAtUtc).First())
                    .OrderBy(record => record.Run)
                    .ToArray();
                var cardRecord = attempts
                    .Where(record => !string.IsNullOrWhiteSpace(record.TaskPrompt))
                    .OrderByDescending(record => record.ObservedAtUtc)
                    .First();
                return new ComplexityHistorySample
                {
                    Card = new ComplexityCard
                    {
                        TaskKey = cardRecord.TaskKey, Prompt = cardRecord.TaskPrompt!, Project = cardRecord.Project,
                        Area = cardRecord.Area, TaskType = cardRecord.TaskType, EpicContext = cardRecord.EpicContext,
                        AcceptanceCriteria = cardRecord.AcceptanceCriteria, ReferencedFiles = cardRecord.ReferencedFiles,
                        ReferencedSubsystems = cardRecord.ReferencedSubsystems, DependencyFanOut = cardRecord.DependencyFanOut,
                        ExpectedChangedLines = cardRecord.ExpectedChangedLines, RepositoryFileCount = cardRecord.RepositoryFileCount,
                        RoutingSignals = cardRecord.RoutingSignals, HardFloorTriggers = cardRecord.HardFloorTriggers,
                    },
                    ActualTokens = attempts.Sum(record =>
                        record.TokenUsageAvailable
                            ? record.Usage.Input + record.Usage.Output + record.Usage.CacheRead + record.Usage.CacheWrite
                            : 0),
                    ActualDuration = TimeSpan.FromTicks(attempts.Sum(record =>
                        record.StartedAtUtc is { } started && record.ExecutedAtUtc >= started
                            ? (record.ExecutedAtUtc - started).Ticks : 0)),
                    // Run numbers preserve known prior attempts even when task storage only retains
                    // the latest run; distinct records provide the same fact when all attempts exist.
                    ReissueCount = attempts.All(record => record.SemanticReissue is not null)
                        ? attempts.Count(record => record.SemanticReissue == true)
                        : Math.Max(attempts.Length - 1, Math.Max(0, attempts.Max(record => record.Run) - 1)),
                    TokenHistoryComplete = attempts.All(record => record.TokenUsageAvailable),
                    DurationHistoryComplete = attempts.All(record => record.StartedAtUtc is { } started && record.ExecutedAtUtc >= started),
                    ReissueHistoryAvailable = attempts.All(record => record.Run > 0),
                    KnownGradeCount = attempts.Count(record => record.Grade is not null),
                    FavorableGradeCount = attempts.Count(record => record.Grade is "A" or "B"),
                    SemanticReissueCount = attempts.All(record => record.SemanticReissue is not null)
                        ? attempts.Count(record => record.SemanticReissue == true)
                        : null,
                };
            })
            .OrderBy(sample => sample.Card.TaskKey, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>Writes imported records. Implementations must replace the record with the same task key and run rather than append it.</summary>
public interface IAgentStudioRunStore
{
    void Upsert(AgentStudioRunRecord record);
    IReadOnlyCollection<AgentStudioRunRecord> Records { get; }
}

/// <summary>Persists immutable attempt routing decisions before a launch is admitted.</summary>
public interface IAgentStudioRoutingDecisionStore
{
    void RecordDecision(AgentStudioRoutingDecisionRecord decision);
    IReadOnlyCollection<AgentStudioRoutingDecisionRecord> Decisions { get; }
}

/// <summary>Host ledger for immutable decisions and append-only attempt evidence.</summary>
public interface IAgentStudioRunLedger : IAgentStudioRunStore, IAgentStudioRoutingDecisionStore
{
    IReadOnlyCollection<AgentStudioRunOutcomeObservation> OutcomeObservations { get; }
    IReadOnlyCollection<AgentStudioOutcomeClassification> OutcomeClassifications { get; }
}

/// <summary>Small in-memory store useful to hosts, tests, and command-line import jobs.</summary>
public sealed class InMemoryAgentStudioRunStore : IAgentStudioRunLedger
{
    private readonly Dictionary<(string TaskKey, int Run), AgentStudioRunRecord> _records = new();
    private readonly Dictionary<string, AgentStudioRoutingDecisionRecord> _decisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AgentStudioRunOutcomeObservation> _observations = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ObservationId, int Version), AgentStudioOutcomeClassification> _classifications = new();
    public IReadOnlyCollection<AgentStudioRunRecord> Records => _records.Values;
    public IReadOnlyCollection<AgentStudioRoutingDecisionRecord> Decisions => _decisions.Values;
    public IReadOnlyCollection<AgentStudioRunOutcomeObservation> OutcomeObservations => _observations.Values;
    public IReadOnlyCollection<AgentStudioOutcomeClassification> OutcomeClassifications => _classifications.Values;

    public void Upsert(AgentStudioRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.RoutingDecision is { } decision)
            RecordDecision(decision);
        if (record.OutcomeObservation is { } observation)
            AddOrVerify(_observations, observation.ObservationId, observation, "outcome observation", record);
        if (record.OutcomeClassification is { } classification)
            AddOrVerify(_classifications, (classification.ObservationId, classification.Version),
                classification, "outcome classification", record);

        var key = (record.TaskKey, record.Run);
        if (!_records.TryGetValue(key, out var current) || record.ObservedAtUtc >= current.ObservedAtUtc)
            _records[key] = record;
    }

    public void RecordDecision(AgentStudioRoutingDecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (_decisions.TryGetValue(decision.DecisionId, out var retained)
            && !SameDecision(retained, decision))
            throw new ArgumentException($"Routing decision '{decision.DecisionId}' cannot be rewritten.", nameof(decision));
        _decisions.TryAdd(decision.DecisionId, decision);
    }

    private static bool SameDecision(AgentStudioRoutingDecisionRecord left, AgentStudioRoutingDecisionRecord right)
        => left.AppliedHardFloorIds.SequenceEqual(right.AppliedHardFloorIds, StringComparer.Ordinal)
            && left with
            {
                AppliedHardFloorIds = Array.Empty<string>(),
                ProvenanceReference = null,
                ProvenanceSha256 = null,
            }
            == right with
            {
                AppliedHardFloorIds = Array.Empty<string>(),
                ProvenanceReference = null,
                ProvenanceSha256 = null,
            };

    private static void AddOrVerify<TKey, TValue>(
        IDictionary<TKey, TValue> destination,
        TKey key,
        TValue value,
        string kind,
        AgentStudioRunRecord record)
        where TKey : notnull
        where TValue : notnull
    {
        if (destination.TryGetValue(key, out var retained) && !EqualityComparer<TValue>.Default.Equals(retained, value))
            throw new ArgumentException($"Append-only {kind} '{key}' has conflicting content.", nameof(record));
        destination.TryAdd(key, value);
    }
}

/// <summary>Cost coverage retained when imported runs are aggregated.</summary>
public sealed record RunCostStatusSummary(
    int ResolvedRuns,
    int UnconfirmedRuns,
    int UnknownModelRuns,
    int NoPriceForDateRuns,
    int UsageUnavailableRuns)
{
    /// <summary>True only when every run has a confirmed catalog price.</summary>
    public bool IsFullyPriced => ResolvedRuns > 0 && UnconfirmedRuns == 0 && UnknownModelRuns == 0 && NoPriceForDateRuns == 0 && UsageUnavailableRuns == 0;
}

/// <summary>A consumption/outcome view grouped by day, provider, CLI, model, and project.</summary>
public sealed record ModelRunView
{
    public required DateOnly Day { get; init; }
    public string? Provider { get; init; }
    /// <summary>Retained so provider availability is not collapsed across different launch surfaces.</summary>
    public string? CliType { get; init; }
    public string? Model { get; init; }
    public string? ThinkingLevel { get; init; }
    public string? PolicyVersion { get; init; }
    public int? OutcomeClassificationVersion { get; init; }
    public string? Project { get; init; }
    public required int Runs { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
    /// <summary>
    /// Null when any aggregated run is unresolved. A measured, fully priced zero may remain zero.
    /// Inspect <see cref="CostStatus"/> to distinguish those cases.
    /// </summary>
    public decimal? CostEstimate { get; init; }
    public required RunCostStatusSummary CostStatus { get; init; }
    public required int SuccessfulRuns { get; init; }
    public required int NeedsReviewRuns { get; init; }
    public required int UnsuccessfulRuns { get; init; }
    public required int ReviewOutcomeAvailableRuns { get; init; }
    public required IReadOnlyDictionary<AgentStudioAttemptOutcomeCategory, int> OutcomeCategoryCounts { get; init; }
}

/// <summary>Builds model-over-time and per-project views from imported run records.</summary>
public static class ModelRunViews
{
    /// <summary>Consumption and outcome per model over time, optionally narrowed to a project.</summary>
    public static IReadOnlyList<ModelRunView> ByModelOverTime(IEnumerable<AgentStudioRunRecord> records, string? project = null)
        => Build(records.Where(r => project is null || string.Equals(r.Project, project, StringComparison.Ordinal)));

    /// <summary>Consumption and outcome per project (with model and day retained for drill-down).</summary>
    public static IReadOnlyList<ModelRunView> ByProject(IEnumerable<AgentStudioRunRecord> records)
        => Build(records);

    private static IReadOnlyList<ModelRunView> Build(IEnumerable<AgentStudioRunRecord> records) => records
        .GroupBy(r => (Day: DateOnly.FromDateTime(r.ObservedAtUtc), r.Provider, r.CliType,
            Model: r.ActualModel, Thinking: r.ActualThinkingLevel, Policy: r.RoutingPolicyVersion,
            ClassificationVersion: r.OutcomeClassification?.Version, r.Project))
        .OrderBy(g => g.Key.Day).ThenBy(g => g.Key.Project).ThenBy(g => g.Key.Model, StringComparer.Ordinal)
        .ThenBy(g => g.Key.Thinking, StringComparer.Ordinal).ThenBy(g => g.Key.Policy, StringComparer.Ordinal)
        .Select(g => new ModelRunView
        {
            Day = g.Key.Day, Provider = g.Key.Provider, CliType = g.Key.CliType, Model = g.Key.Model,
            ThinkingLevel = g.Key.Thinking, PolicyVersion = g.Key.Policy,
            OutcomeClassificationVersion = g.Key.ClassificationVersion, Project = g.Key.Project,
            Runs = g.Count(), InputTokens = g.Sum(r => r.Usage.Input), OutputTokens = g.Sum(r => r.Usage.Output),
            CacheReadTokens = g.Sum(r => r.Usage.CacheRead), CacheWriteTokens = g.Sum(r => r.Usage.CacheWrite),
            CostEstimate = g.Any(r => r.CostEstimate is null) ? null : g.Sum(r => r.CostEstimate!.Value),
            CostStatus = new(
                g.Count(r => r.CostStatus == PriceStatus.Resolved && !r.CostUnconfirmed),
                g.Count(r => r.CostStatus == PriceStatus.Resolved && r.CostUnconfirmed),
                g.Count(r => r.CostStatus == PriceStatus.UnknownModel),
                g.Count(r => r.CostStatus == PriceStatus.NoPriceForDate),
                g.Count(r => r.CostStatus == PriceStatus.UsageUnavailable)),
            SuccessfulRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.Successful),
            NeedsReviewRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.NeedsReview),
            UnsuccessfulRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.Unsuccessful),
            ReviewOutcomeAvailableRuns = g.Count(r => r.ReviewOutcome != AgentStudioReviewOutcome.Unknown),
            OutcomeCategoryCounts = Enum.GetValues<AgentStudioAttemptOutcomeCategory>()
                .ToDictionary(category => category, category => g.Count(r => r.OutcomeCategory == category)),
        }).ToList();
}
