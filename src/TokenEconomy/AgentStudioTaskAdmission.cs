using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public records mirror the documented Agent Studio host boundary.

/// <summary>One Agent Studio admission request. Capacity is an immutable snapshot for this run.</summary>
public sealed record AgentStudioTaskAdmissionRequest
{
    public required ComplexityCard Task { get; init; }
    public required int Run { get; init; }
    public required ModelRoutingCapacity Capacity { get; init; }
    public IReadOnlyCollection<Cli> AvailableClis { get; init; } = [];
    public RoutingEvidenceReport? BenchmarkQualification { get; init; }
    public string? RequiredBenchmarkCapability { get; init; }
    public IReadOnlyList<ModelTrustAssessment> TrustEvidence { get; init; } = [];
    public ModelRoutingWorkflow Workflow { get; init; } = new();
    /// <summary>An explicit operator pin. This is distinct from the card's configured default route.</summary>
    public OperatorModelRoutePin? OperatorPin { get; init; }
    /// <summary>Card configuration retained for display and audit; the adapter never mutates it.</summary>
    public OperatorModelRoutePin? CardConfiguredRoute { get; init; }
}

/// <summary>The attempt-local route passed to a launcher only after admission selected a safe route.</summary>
public sealed record AgentStudioAttemptLaunchRoute(
    string DecisionId,
    string TaskKey,
    int Run,
    string ModelId,
    string ThinkingLevel,
    Cli Cli);

/// <summary>Complete admission output. A null launch route means the host must not launch.</summary>
public sealed record AgentStudioTaskAdmissionDecision
{
    public required TaskComplexityEstimate Features { get; init; }
    public required ModelRoutingResult Routing { get; init; }
    public required AgentStudioRoutingDecisionRecord PersistedDecision { get; init; }
    public AgentStudioAttemptLaunchRoute? LaunchRoute { get; init; }
    public bool CanLaunch => LaunchRoute is not null;
}

/// <summary>
/// Agent Studio host adapter for the full task-to-outcome loop. It persists intake features once,
/// routes every attempt against that attempt's quota snapshot, consumes the newest classified prior
/// outcome, records the immutable decision, and emits an attempt-local launch route without changing
/// card configuration. Outcome ingestion remains the task-storage importer's append-only boundary.
/// </summary>
public sealed class AgentStudioTaskAdmission
{
    private readonly TaskComplexityEstimator _estimator;
    private readonly ModelRouter _router;
    private readonly ITaskComplexityEstimateStore _estimateStore;
    private readonly IAgentStudioRunLedger _ledger;

    public AgentStudioTaskAdmission(
        ITaskComplexityEstimateStore estimateStore,
        IAgentStudioRunLedger ledger,
        TaskComplexityEstimator? estimator = null,
        ModelRouter? router = null)
    {
        _estimateStore = estimateStore ?? throw new ArgumentNullException(nameof(estimateStore));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _estimator = estimator ?? new TaskComplexityEstimator();
        _router = router ?? ModelRouter.Default;
    }

    /// <summary>Estimate if needed, route, persist, and return either a launch route or a closed admission.</summary>
    public AgentStudioTaskAdmissionDecision PrepareAttempt(AgentStudioTaskAdmissionRequest request)
    {
        Validate(request);
        var estimate = _estimateStore.Estimates.SingleOrDefault(item =>
            string.Equals(item.TaskKey, request.Task.TaskKey, StringComparison.Ordinal));
        if (estimate is null)
        {
            estimate = _estimator.Estimate(request.Task, ComplexityHistory.FromRunRecords(_ledger.Records));
            _estimateStore.Upsert(estimate);
        }

        var priorEvidence = ClassifiedPriorAttempts(request.Task.TaskKey, request.Run);
        var prior = priorEvidence.OrderByDescending(item => item.ObservedAtUtc)
            .ThenByDescending(item => item.Run).FirstOrDefault();
        var strongerTierFailures = priorEvidence.Count(item =>
            item.Classification.IsSemanticFailure && item.Decision?.SemanticPromotionApplied == true);

        var routing = _router.Route(new()
        {
            Task = request.Task,
            UpfrontEstimate = estimate,
            Capacity = request.Capacity,
            AvailableClis = request.AvailableClis,
            BenchmarkQualification = request.BenchmarkQualification,
            RequiredBenchmarkCapability = request.RequiredBenchmarkCapability,
            TrustEvidence = request.TrustEvidence,
            Workflow = request.Workflow,
            OperatorPin = request.OperatorPin,
            PreviousOutcome = prior?.Classification.RoutingOutcome ?? RoutingAttemptOutcome.None,
            SemanticFailuresAtStrongerTier = strongerTierFailures,
        });

        var decisionId = $"{request.Task.TaskKey}:attempt:{request.Run}:routing";
        var persisted = ToRecord(decisionId, request, routing);
        _ledger.RecordDecision(persisted);
        var launch = routing.SelectedRoute is { } selected
            ? new AgentStudioAttemptLaunchRoute(decisionId, request.Task.TaskKey, request.Run,
                selected.ModelId, selected.ThinkingLevel, selected.Cli)
            : null;
        return new()
        {
            Features = estimate,
            Routing = routing,
            PersistedDecision = persisted,
            LaunchRoute = launch,
        };
    }

    private IReadOnlyList<ClassifiedAttemptEvidence> ClassifiedPriorAttempts(string taskKey, int run)
    {
        var decisions = _ledger.Decisions.ToDictionary(item => item.DecisionId, StringComparer.Ordinal);
        var classifications = _ledger.OutcomeClassifications
            .GroupBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Version).First(),
                StringComparer.Ordinal);
        return _ledger.OutcomeObservations
            .Where(observation => string.Equals(observation.TaskKey, taskKey, StringComparison.Ordinal)
                && observation.Run < run && classifications.ContainsKey(observation.ObservationId))
            .Select(observation => new ClassifiedAttemptEvidence(
                observation.Run,
                observation.ObservedAtUtc,
                classifications[observation.ObservationId],
                decisions.GetValueOrDefault(observation.DecisionId)))
            .GroupBy(item => item.Run)
            .Select(group => group.OrderByDescending(item => item.ObservedAtUtc)
                .ThenByDescending(item => item.Classification.Version).First())
            .OrderBy(item => item.Run)
            .ToArray();
    }

    private static AgentStudioRoutingDecisionRecord ToRecord(
        string decisionId,
        AgentStudioTaskAdmissionRequest request,
        ModelRoutingResult routing)
    {
        var fallback = routing.SelectionSource is ModelRouteSelectionSource.EquivalentProviderFallback
            or ModelRouteSelectionSource.OneTierQuotaDowngrade;
        var pinWarning = routing.OperatorPin is null ? null
            : routing.OperatorPinBelowPolicy
                ? $"Pinned route {routing.OperatorPin.ModelId}/{routing.OperatorPin.ThinkingLevel} is below policy recommendation {routing.RecommendedRoute.ModelId}/{routing.RecommendedRoute.ThinkingLevel}."
                : routing.Disposition == ModelRoutingDisposition.OverrideRequired
                    ? routing.FallbackOrWaitReason
                    : null;
        return new()
        {
            DecisionId = decisionId,
            TaskKey = request.Task.TaskKey,
            Run = request.Run,
            Disposition = routing.Disposition,
            PolicyVersion = routing.PolicyVersion,
            RecommendedRouteId = routing.RecommendedRoute.RouteId,
            RecommendedModel = routing.RecommendedRoute.ModelId,
            RecommendedThinkingLevel = routing.RecommendedRoute.ThinkingLevel,
            RecommendedRouteProvisional = routing.RecommendedRoute.Provisional,
            SelectedRouteId = routing.SelectedRoute?.RouteId,
            SelectedModel = routing.SelectedRoute?.ModelId,
            SelectedThinkingLevel = routing.SelectedRoute?.ThinkingLevel,
            SelectedRouteProvisional = routing.SelectedRoute?.Provisional,
            SelectionSource = routing.SelectionSource.ToString(),
            Score = routing.ScoreWorksheet.EffectivePolicyScore,
            UpfrontScore = routing.ScoreWorksheet.Total,
            HardFloorRouteId = routing.CorrectnessFloor.RouteId,
            HardFloorModel = routing.CorrectnessFloor.ModelId,
            HardFloorThinkingLevel = routing.CorrectnessFloor.ThinkingLevel,
            AppliedHardFloorIds = routing.CorrectnessFloor.AppliedFloorIds,
            IsHardFloor = routing.CorrectnessFloor.IsHardFloor,
            SemanticPromotionApplied = routing.CorrectnessFloor.AppliedFloorIds.Contains(
                "semanticReissuePromotion", StringComparer.Ordinal),
            ConfiguredModel = request.CardConfiguredRoute?.ModelId,
            ConfiguredThinkingLevel = request.CardConfiguredRoute?.ThinkingLevel,
            OperatorPinModel = routing.OperatorPin?.ModelId,
            OperatorPinThinkingLevel = routing.OperatorPin?.ThinkingLevel,
            OperatorPinBelowPolicy = routing.OperatorPin is null ? null : routing.OperatorPinBelowPolicy,
            OperatorPinWarning = pinWarning,
            QuotaFallbackApplied = fallback,
            QuotaFallbackReason = fallback ? routing.FallbackOrWaitReason : null,
            WaitOrOverrideReason = routing.Disposition == ModelRoutingDisposition.Selected
                ? null : routing.FallbackOrWaitReason,
            QuotaSnapshotDecisionAtUtc = request.Capacity.ProviderAvailability.DecisionAtUtc,
            QuotaSnapshotId = SnapshotId(request.Capacity.ProviderAvailability),
            PolicyReason = routing.PolicyReason,
            Reason = routing.FallbackOrWaitReason,
            DecidedAtUtc = request.Capacity.ProviderAvailability.DecisionAtUtc,
        };
    }

    private static string SnapshotId(ProviderAvailabilitySnapshot snapshot)
    {
        var canonical = new StringBuilder()
            .Append(snapshot.DecisionAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        foreach (var provider in snapshot.Providers.OrderBy(row => row.Provider, StringComparer.Ordinal)
                     .ThenBy(row => row.CliType, StringComparer.Ordinal))
        {
            canonical.Append(provider.Provider).Append('|').Append(provider.CliType).Append('|')
                .Append(provider.Availability).Append('|').Append(provider.Freshness).Append('|')
                .Append(provider.WarningState).Append('\n');
            foreach (var window in provider.QuotaWindows.OrderBy(item => item.WindowId, StringComparer.Ordinal))
                canonical.Append(window.WindowId).Append('|').Append(window.Freshness).Append('|')
                    .Append(window.WarningState).Append('|')
                    .Append(window.Usage?.UsedTokens.ToString(CultureInfo.InvariantCulture) ?? "unknown").Append('|')
                    .Append(window.Usage?.LimitTokens.ToString(CultureInfo.InvariantCulture) ?? "unknown").Append('\n');
        }
        return "quota-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Validate(AgentStudioTaskAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Task);
        ArgumentNullException.ThrowIfNull(request.Capacity);
        ArgumentNullException.ThrowIfNull(request.Capacity.ProviderAvailability);
        if (request.Run <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Run must be positive.");
        if (request.Capacity.ProviderAvailability.DecisionAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The run-scoped quota decision time must be UTC.", nameof(request));
    }

    private sealed record ClassifiedAttemptEvidence(
        int Run,
        DateTime ObservedAtUtc,
        AgentStudioOutcomeClassification Classification,
        AgentStudioRoutingDecisionRecord? Decision);
}

#pragma warning restore CS1591
