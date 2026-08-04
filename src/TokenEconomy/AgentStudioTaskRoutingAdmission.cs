using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public records intentionally mirror the Agent Studio host contract.

/// <summary>One attempt-scoped admission request. The configured card route is audit data, not a mutable launch setting.</summary>
public sealed record AgentStudioTaskLaunchAdmissionRequest
{
    public required ComplexityCard Task { get; init; }
    public required int Run { get; init; }
    public required ModelRoutingCapacity Capacity { get; init; }
    public IReadOnlyCollection<Cli> AvailableClis { get; init; } = [];
    public RoutingEvidenceReport? BenchmarkQualification { get; init; }
    public string? RequiredBenchmarkCapability { get; init; }
    public IReadOnlyList<ModelTrustAssessment> TrustEvidence { get; init; } = [];
    public ModelRoutingWorkflow Workflow { get; init; } = new();
    public OperatorModelRoutePin? HumanOverride { get; init; }
    public string? ConfiguredModel { get; init; }
    public string? ConfiguredThinkingLevel { get; init; }
    public string? DecisionId { get; init; }
}

/// <summary>Host-facing launch/wait result. A null launch route means the host must not start an attempt.</summary>
public sealed record AgentStudioTaskLaunchAdmission
{
    public required AgentStudioRoutingDecisionRecord Decision { get; init; }
    public required ModelRoutingResult Routing { get; init; }
    public ModelRoutingSelectedRoute? LaunchRoute => Routing.SelectedRoute;
    public bool MayLaunch => Routing.Disposition == ModelRoutingDisposition.Selected && LaunchRoute is not null;
}

/// <summary>
/// Agent Studio integration boundary for the complete pre-launch loop. It reads the durable estimate,
/// applies the newest classified evidence, routes against this run's quota snapshot, and persists the
/// immutable decision before returning a launch route.
/// </summary>
public sealed class AgentStudioTaskRoutingAdmission
{
    private readonly ITaskComplexityEstimateStore _estimates;
    private readonly IAgentStudioRunLedger _runs;
    private readonly ModelRouter _router;
    private readonly ModelRoutingKnowledgeBase _knowledge;

    public AgentStudioTaskRoutingAdmission(
        ITaskComplexityEstimateStore estimates,
        IAgentStudioRunLedger runs,
        ModelRouter? router = null)
    {
        _estimates = estimates ?? throw new ArgumentNullException(nameof(estimates));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _router = router ?? ModelRouter.Default;
        _knowledge = _router.Knowledge;
    }

    /// <summary>Route and persist one attempt before launch. Replaying identical input is idempotent.</summary>
    public AgentStudioTaskLaunchAdmission Admit(AgentStudioTaskLaunchAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Task);
        ArgumentNullException.ThrowIfNull(request.Capacity);
        ArgumentNullException.ThrowIfNull(request.Capacity.ProviderAvailability);
        if (request.Run < 1) throw new ArgumentOutOfRangeException(nameof(request), "Attempt run must be at least one.");
        if (string.IsNullOrWhiteSpace(request.Task.TaskKey)) throw new ArgumentException("Task key is required.", nameof(request));
        if ((request.ConfiguredModel is null) != (request.ConfiguredThinkingLevel is null))
            throw new ArgumentException("Configured model and thinking level must be supplied together.", nameof(request));

        var matches = _estimates.Estimates.Where(estimate =>
            string.Equals(estimate.TaskKey, request.Task.TaskKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? $"No stored upfront complexity estimate exists for task '{request.Task.TaskKey}'."
                : $"Multiple stored upfront complexity estimates exist for task '{request.Task.TaskKey}'.");

        var prior = NewestClassifiedEvidence(request.Task.TaskKey, request.Run);
        var routing = _router.Route(new()
        {
            Task = request.Task,
            UpfrontEstimate = matches[0],
            Capacity = request.Capacity,
            AvailableClis = request.AvailableClis,
            BenchmarkQualification = request.BenchmarkQualification,
            RequiredBenchmarkCapability = request.RequiredBenchmarkCapability,
            TrustEvidence = request.TrustEvidence,
            Workflow = request.Workflow,
            OperatorPin = request.HumanOverride,
            PreviousOutcome = prior?.Classification.RoutingOutcome ?? RoutingAttemptOutcome.None,
            SemanticFailuresAtStrongerTier = SemanticFailuresAtStrongerTier(request.Task.TaskKey, request.Run, matches[0]),
        });

        var decision = Decision(request, routing);
        _runs.AddDecision(decision); // durable before the caller receives permission to launch
        return new() { Decision = decision, Routing = routing };
    }

    private (AgentStudioRunOutcomeObservation Observation, AgentStudioOutcomeClassification Classification)?
        NewestClassifiedEvidence(string taskKey, int run)
    {
        var candidates = ClassifiedEvidence(taskKey, run)
            .OrderByDescending(item => item.Observation.ObservedAtUtc)
            .ThenByDescending(item => item.Observation.Run)
            .ThenByDescending(item => item.Classification.Version)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[0];
    }

    private int SemanticFailuresAtStrongerTier(string taskKey, int run, TaskComplexityEstimate estimate)
    {
        var baseRoute = _knowledge.Routes.Single(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask
            && estimate.Score >= route.MinimumScore && estimate.Score <= route.MaximumScore);
        var decisions = _runs.Decisions.Where(decision => decision.Run < run
                && string.Equals(decision.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(decision => decision.DecisionId, StringComparer.Ordinal);

        return ClassifiedEvidence(taskKey, run)
            .Where(item => item.Classification.IsSemanticFailure
                && decisions.TryGetValue(item.Observation.DecisionId, out var decision)
                && DecisionRank(decision) > baseRoute.Rank)
            .Count();
    }

    private IReadOnlyList<(AgentStudioRunOutcomeObservation Observation, AgentStudioOutcomeClassification Classification)>
        ClassifiedEvidence(string taskKey, int run)
        => _runs.OutcomeObservations
            .Where(observation => observation.Run < run
                && string.Equals(observation.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase))
            .Select(observation => (Observation: observation, Classification: _runs.OutcomeClassifications
                .Where(classification => classification.ObservationId == observation.ObservationId)
                .OrderByDescending(classification => classification.Version).FirstOrDefault()))
            .Where(item => item.Classification is not null)
            .Select(item => (item.Observation, Classification: item.Classification!))
            .GroupBy(item => item.Observation.Run)
            .Select(group => group.OrderByDescending(item => item.Observation.ObservedAtUtc)
                .ThenByDescending(item => item.Classification.Version).First())
            .ToArray();

    private int DecisionRank(AgentStudioRoutingDecisionRecord decision)
    {
        if (_knowledge.FindRoute(decision.RecommendedRouteId) is { WorkflowRole: RoutingWorkflowRole.CoreTask } route)
            return route.Rank;
        var recommended = _knowledge.Routes.FirstOrDefault(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask
            && string.Equals(route.ModelId, decision.RecommendedModel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(route.ThinkingLevel, decision.RecommendedThinkingLevel, StringComparison.OrdinalIgnoreCase));
        if (recommended is not null) return recommended.Rank;
        if (_knowledge.FindRoute(decision.SelectedRouteId) is { WorkflowRole: RoutingWorkflowRole.CoreTask } selected)
            return selected.Rank;
        var direct = _knowledge.Routes.FirstOrDefault(candidate => candidate.WorkflowRole == RoutingWorkflowRole.CoreTask
            && string.Equals(candidate.ModelId, decision.SelectedModel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ThinkingLevel, decision.SelectedThinkingLevel, StringComparison.OrdinalIgnoreCase));
        if (direct is not null) return direct.Rank;
        return -1;
    }

    private static AgentStudioRoutingDecisionRecord Decision(
        AgentStudioTaskLaunchAdmissionRequest request,
        ModelRoutingResult routing)
    {
        var source = routing.SelectionSource.ToString();
        var quotaFallback = routing.SelectionSource is ModelRouteSelectionSource.EquivalentProviderFallback
            or ModelRouteSelectionSource.OneTierQuotaDowngrade;
        var snapshot = request.Capacity.ProviderAvailability;
        return new()
        {
            DecisionId = string.IsNullOrWhiteSpace(request.DecisionId)
                ? $"{request.Task.TaskKey}:attempt:{request.Run}:routing"
                : request.DecisionId,
            TaskKey = request.Task.TaskKey,
            Run = request.Run,
            PolicyVersion = routing.PolicyVersion,
            Disposition = routing.Disposition.ToString(),
            ConfiguredModel = request.ConfiguredModel,
            ConfiguredThinkingLevel = request.ConfiguredThinkingLevel,
            RecommendedRouteId = routing.RecommendedRoute.RouteId,
            RecommendedModel = routing.RecommendedRoute.ModelId,
            RecommendedThinkingLevel = routing.RecommendedRoute.ThinkingLevel,
            RecommendedProvisional = routing.RecommendedRoute.Provisional,
            SelectedRouteId = routing.SelectedRoute?.RouteId,
            SelectedModel = routing.SelectedRoute?.ModelId,
            SelectedThinkingLevel = routing.SelectedRoute?.ThinkingLevel,
            SelectedProvisional = routing.SelectedRoute?.Provisional,
            SelectionSource = source,
            UpfrontScore = routing.ScoreWorksheet.Total,
            Score = routing.ScoreWorksheet.EffectivePolicyScore,
            HardFloorRouteId = routing.CorrectnessFloor.RouteId,
            HardFloorModel = routing.CorrectnessFloor.ModelId,
            HardFloorThinkingLevel = routing.CorrectnessFloor.ThinkingLevel,
            IsHardFloor = routing.CorrectnessFloor.IsHardFloor,
            AppliedHardFloorIds = routing.CorrectnessFloor.AppliedFloorIds,
            Reason = routing.PolicyReason,
            SelectionReason = routing.FallbackOrWaitReason,
            QuotaFallback = quotaFallback,
            QuotaSnapshotAtUtc = snapshot.DecisionAtUtc,
            QuotaSnapshotState = string.Join(";", snapshot.Providers
                .OrderBy(row => row.Provider, StringComparer.Ordinal)
                .ThenBy(row => row.CliType, StringComparer.Ordinal)
                .Select(row => $"{row.Provider}/{row.CliType}:{row.WarningState}/{row.Freshness}")),
            OperatorPinBelowPolicy = routing.OperatorPinBelowPolicy,
            PinWarning = routing.OperatorPinBelowPolicy ? routing.FallbackOrWaitReason : null,
            WaitReason = routing.Disposition == ModelRoutingDisposition.Selected ? null : routing.FallbackOrWaitReason,
            DecidedAtUtc = snapshot.DecisionAtUtc,
        };
    }
}

/// <summary>Dependency-free operator view for one persisted admission decision.</summary>
public static class AgentStudioRoutingDecisionHtmlRenderer
{
    public static string Render(AgentStudioRoutingDecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var recommended = Route(decision.RecommendedModel, decision.RecommendedThinkingLevel);
        var selected = Route(decision.SelectedModel, decision.SelectedThinkingLevel);
        var hardFloor = Route(decision.HardFloorModel, decision.HardFloorThinkingLevel);
        var provisional = decision.SelectedProvisional ?? decision.RecommendedProvisional;
        var html = new StringBuilder("<article class=\"routing-decision\" aria-label=\"Task routing decision\"><header><h2>Routing decision ")
            .Append(Escape(decision.TaskKey)).Append(" · attempt ").Append(decision.Run.ToString(CultureInfo.InvariantCulture))
            .Append("</h2><p class=\"disposition\">").Append(Escape(decision.Disposition ?? "Unknown")).Append("</p></header><dl>")
            .Append(Fact("Recommended route", recommended))
            .Append(Fact("Selected route", selected))
            .Append(Fact("Score", decision.Score?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"))
            .Append(Fact("Hard floor", hardFloor + (decision.IsHardFloor == true ? " (applied)" : " (baseline)")))
            .Append(Fact("Selection source", decision.SelectionSource ?? "Unknown"))
            .Append(Fact("Selection explanation", decision.SelectionReason ?? "Unknown"))
            .Append(Fact("Policy version", decision.PolicyVersion ?? "Unknown"))
            .Append(Fact("Provisional status", provisional is null ? "Unknown" : provisional.Value ? "Provisional" : "Established"))
            .Append(Fact("Quota fallback", decision.QuotaFallback ? "Yes" : "No"))
            .Append(Fact("Pin warning", decision.PinWarning ?? "None"))
            .Append(Fact("Wait reason", decision.WaitReason ?? "None"));
        if (decision.ConfiguredModel is not null)
            html.Append(Fact("Card configured route (unchanged)", Route(decision.ConfiguredModel, decision.ConfiguredThinkingLevel)));
        return html.Append("</dl><p class=\"policy-reason\">").Append(Escape(decision.Reason ?? "No policy explanation recorded."))
            .Append("</p></article>").ToString();
    }

    private static string Fact(string label, string value) => $"<dt>{Escape(label)}</dt><dd>{Escape(value)}</dd>";
    private static string Route(string? model, string? thinking) => model is null ? "No safe route" : $"{model} / {thinking ?? "unknown"}";
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}

#pragma warning restore CS1591
