namespace TokenEconomy;

#pragma warning disable CS1591 // Public records intentionally mirror the routing audit contract.

/// <summary>The terminal disposition of a deterministic model-routing decision.</summary>
public enum ModelRoutingDisposition { Selected, Wait, OverrideRequired }

/// <summary>The source that selected a route, or explains why none was selected.</summary>
public enum ModelRouteSelectionSource
{
    PolicyRecommendation,
    EquivalentProviderFallback,
    OneTierQuotaDowngrade,
    OperatorPin,
    WaitForSafeRoute,
    OverrideRequired,
}

/// <summary>Workflow facts that constrain which policy role may execute the task.</summary>
public sealed record ModelRoutingWorkflow
{
    public RoutingWorkflowRole Role { get; init; } = RoutingWorkflowRole.CoreTask;
    public bool EvidenceIsCompactAndStructured { get; init; }
    public bool HasDeterministicOutputContract { get; init; }
    public bool EvidenceIsAmbiguous { get; init; }
    public bool EvidenceIsUnbounded { get; init; }
    public IReadOnlyList<string> AuthorizingTriggers { get; init; } = [];
}

/// <summary>Run-scoped quota and budget facts consulted only after score and floor selection.</summary>
public sealed record ModelRoutingCapacity
{
    public required ProviderAvailabilitySnapshot ProviderAvailability { get; init; }
    public BudgetPressure BudgetPressure { get; init; } = BudgetPressure.Comfortable;
    public bool DeterministicVerificationAvailable { get; init; }
}

/// <summary>An explicit operator model/thinking-level pin.</summary>
public sealed record OperatorModelRoutePin(string ModelId, string ThinkingLevel);

/// <summary>All inputs to the deterministic routing composition.</summary>
public sealed record ModelRoutingSelectionRequest
{
    public required ComplexityCard Task { get; init; }
    public required TaskComplexityEstimate UpfrontEstimate { get; init; }
    public required ModelRoutingCapacity Capacity { get; init; }
    public IReadOnlyCollection<Cli> AvailableClis { get; init; } = [];
    public RoutingEvidenceReport? BenchmarkQualification { get; init; }
    public string? RequiredBenchmarkCapability { get; init; }
    public IReadOnlyList<ModelTrustAssessment> TrustEvidence { get; init; } = [];
    public ModelRoutingWorkflow Workflow { get; init; } = new();
    public OperatorModelRoutePin? OperatorPin { get; init; }
    public RoutingAttemptOutcome PreviousOutcome { get; init; }
    public int SemanticFailuresAtStrongerTier { get; init; }
}

/// <summary>One scored criterion retained verbatim from the upfront estimate.</summary>
public sealed record ModelRoutingScoreItem(string Id, int Score, int MaximumScore, string Evidence);

/// <summary>The complete score worksheet used before runtime quota selection.</summary>
public sealed record ModelRoutingScoreWorksheet
{
    public required ModelRoutingScorecard Scorecard { get; init; }
    public required IReadOnlyList<ModelRoutingScoreItem> Criteria { get; init; }
    public required int Total { get; init; }
    public required int EffectivePolicyScore { get; init; }
    public required string Evidence { get; init; }
}

/// <summary>The hard correctness floor established independently of quota and cost.</summary>
public sealed record ModelRoutingCorrectnessFloor
{
    public required string RouteId { get; init; }
    public required string ModelId { get; init; }
    public required string ThinkingLevel { get; init; }
    public required IReadOnlyList<string> AppliedFloorIds { get; init; }
    public required bool IsHardFloor { get; init; }
}

/// <summary>A fully resolved route plus compatibility, benchmark, and trust evidence.</summary>
public sealed record ModelRoutingSelectedRoute
{
    public required string RouteId { get; init; }
    public required string ModelId { get; init; }
    public required string ThinkingLevel { get; init; }
    public required Cli Cli { get; init; }
    public required RoutingWorkflowRole WorkflowRole { get; init; }
    public required int PolicyRank { get; init; }
    public required PolicyEvidenceStatus PolicyEvidenceStatus { get; init; }
    public required bool Provisional { get; init; }
    public required ModelSuggestion EfficiencySuggestion { get; init; }
    public RoutingQualification? BenchmarkQualification { get; init; }
    public ModelTrustAssessment? TrustEvidence { get; init; }
}

/// <summary>Visible uncertainty retained with every result, including successful selections.</summary>
public sealed record ModelRoutingUncertainty
{
    public required double UpfrontEstimateConfidence { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public bool IsUncertain => Reasons.Count > 0;
}

/// <summary>The complete, auditable result of <see cref="ModelRouter.Route"/>.</summary>
public sealed record ModelRoutingResult
{
    public required ModelRoutingDisposition Disposition { get; init; }
    public required string TaskKey { get; init; }
    public required string PolicyVersion { get; init; }
    public required int ModelKnowledgeSchemaVersion { get; init; }
    public required string ModelKnowledgeEvidenceVersion { get; init; }
    public required string BenchmarkEvidenceVersion { get; init; }
    public required int? BenchmarkGateVersion { get; init; }
    public required RoutingWorkflowRole WorkflowRole { get; init; }
    public required ModelRoutingSelectedRoute RecommendedRoute { get; init; }
    public ModelRoutingSelectedRoute? SelectedRoute { get; init; }
    public required ModelRoutingScoreWorksheet ScoreWorksheet { get; init; }
    public required ModelRoutingCorrectnessFloor CorrectnessFloor { get; init; }
    public required ModelRouteSelectionSource SelectionSource { get; init; }
    public required string PolicyReason { get; init; }
    public required string FallbackOrWaitReason { get; init; }
    public required ModelRoutingUncertainty Uncertainty { get; init; }
    public OperatorModelRoutePin? OperatorPin { get; init; }
    public required bool OperatorPinBelowPolicy { get; init; }
}

/// <summary>
/// Public, dependency-free composition of policy, model knowledge, efficiency ranking, benchmark
/// qualification, trust evidence, workflow constraints, operator pins, and run-scoped capacity.
/// Score and correctness floors are always established before quota or cost is inspected.
/// </summary>
public sealed class ModelRouter
{
    private readonly ModelRoutingPolicy _policy;
    private readonly ModelRoutingKnowledgeBase _knowledge;
    private readonly ModelEfficiencyMatrix _matrix;
    private readonly IReadOnlyList<ModelRoutingTier> _coreRoutes;

    public ModelRouter(
        ModelRoutingPolicy policy,
        ModelRoutingKnowledgeBase knowledge,
        ModelEfficiencyMatrix matrix)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        if (!ReferenceEquals(policy.Knowledge, knowledge))
            throw new ArgumentException("The policy must be constructed from the supplied model knowledge.", nameof(policy));
        _coreRoutes = knowledge.Routes.Where(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask)
            .OrderBy(route => route.Rank).ToArray();
    }

    public ModelRouter(ModelRoutingKnowledgeBase knowledge, ModelEfficiencyMatrix matrix)
        : this(new ModelRoutingPolicy(knowledge), knowledge, matrix) { }

    public static ModelRouter Default { get; } = new(
        ModelRoutingPolicy.Default, ModelRoutingKnowledgeBase.Default, ModelEfficiencyMatrix.Default);

    /// <summary>Return one selected route, a wait decision, or an explicit-override requirement.</summary>
    public ModelRoutingResult Route(ModelRoutingSelectionRequest request)
    {
        Validate(request);
        var worksheet = Worksheet(request.UpfrontEstimate);
        var taskClass = TaskClassOf(request.Task.TaskType);
        var trust = TrustByModel(request.TrustEvidence);
        var uncertainties = new List<string>();
        if (EvidenceTaskClass(request.Task.TaskType) is null)
            uncertainties.Add("Task class is unknown; benchmark evidence cannot qualify a provider fallback.");

        var policyDecision = Recommendation(request, worksheet);
        worksheet = worksheet with { EffectivePolicyScore = policyDecision.Score };
        var recommended = Candidate(policyDecision.Route.Id, policyDecision.Route, taskClass, request, trust);
        AddUncertainty(recommended, request, uncertainties);
        if (request.UpfrontEstimate.Confidence < 1)
            uncertainties.Add($"Upfront estimate confidence is {request.UpfrontEstimate.Confidence:0.###}, below certainty.");

        var floorRoute = CorrectnessFloor(policyDecision, request.Workflow.Role);
        var floorIds = policyDecision.AppliedHardFloors
            .Concat(policyDecision.ReissuePromoted ? ["semanticReissuePromotion"] : Array.Empty<string>())
            .ToArray();
        var floor = new ModelRoutingCorrectnessFloor
        {
            RouteId = floorRoute.Id,
            ModelId = floorRoute.ModelId,
            ThinkingLevel = floorRoute.ThinkingLevel,
            AppliedFloorIds = floorIds,
            IsHardFloor = floorIds.Length > 0,
        };

        if (request.OperatorPin is { } pin)
        {
            var resolution = _knowledge.Resolve(pin.ModelId, pin.ThinkingLevel, request.Workflow.Role);
            if (!resolution.IsResolved)
            {
                uncertainties.Add(resolution.Reason);
                return Result(ModelRoutingDisposition.OverrideRequired, recommended, null,
                    ModelRouteSelectionSource.OverrideRequired,
                    $"The operator pin was retained but cannot run under the requested workflow: {resolution.Reason}", false);
            }

            var pinned = Candidate($"operator-pin:{resolution.Model!.CanonicalId}/{resolution.ThinkingLevel!.Id}",
                resolution.Model, resolution.ThinkingLevel.Id, request.Workflow.Role, taskClass, request, trust);
            var below = !MeetsRecommendation(pinned, policyDecision.Route);
            if (below)
                uncertainties.Add($"Operator pin {pinned.ModelId}/{pinned.ThinkingLevel} is below policy recommendation {recommended.ModelId}/{recommended.ThinkingLevel}.");
            if (pinned.TrustEvidence?.Level == TrustLevel.Restricted)
                return Result(ModelRoutingDisposition.OverrideRequired, recommended, null,
                    ModelRouteSelectionSource.OverrideRequired,
                    "The pinned model has open material trust evidence and requires an explicit incident override.", below);
            AddUncertainty(pinned, request, uncertainties);
            var pinCapacity = CapacityOf(pinned, request);
            if (pinCapacity.State is RouteCapacityState.Capped or RouteCapacityState.Unknown or RouteCapacityState.Unavailable)
                return Result(ModelRoutingDisposition.Wait, recommended, null, ModelRouteSelectionSource.WaitForSafeRoute,
                    $"The retained operator pin cannot launch safely: {pinCapacity.Reason}", below);
            return Result(ModelRoutingDisposition.Selected, recommended, pinned, ModelRouteSelectionSource.OperatorPin,
                below ? "Operator pin selected below policy and visibly flagged." : "Operator pin selected at or above policy.", below);
        }

        if (policyDecision.RequiresHumanDecision)
            return Result(ModelRoutingDisposition.OverrideRequired, recommended, null,
                ModelRouteSelectionSource.OverrideRequired, policyDecision.Reason, false);

        var recommendedCapacity = CapacityOf(recommended, request);
        var budgetConstrained = request.Capacity.BudgetPressure == BudgetPressure.Critical;
        if (recommendedCapacity.State == RouteCapacityState.Comfortable && !budgetConstrained
            && recommended.TrustEvidence?.Level != TrustLevel.Restricted)
            return Result(ModelRoutingDisposition.Selected, recommended, recommended,
                ModelRouteSelectionSource.PolicyRecommendation, "The recommended route is available with comfortable capacity.", false);

        var fallback = EquivalentFallback(policyDecision.Route, recommended, recommendedCapacity,
            taskClass, request, trust, uncertainties);
        if (fallback is not null)
            return Result(ModelRoutingDisposition.Selected, recommended, fallback,
                ModelRouteSelectionSource.EquivalentProviderFallback,
                $"Equivalent-provider fallback selected before any downgrade because {recommendedCapacity.Reason}", false);

        var downgrade = OneTierDowngrade(policyDecision, recommended, taskClass, request, trust, uncertainties);
        if (downgrade is not null)
            return Result(ModelRoutingDisposition.Selected, recommended, downgrade,
                ModelRouteSelectionSource.OneTierQuotaDowngrade,
                $"One-tier downgrade selected at score {worksheet.Total}: within five points of the lower threshold, no hard floor, and deterministic verification is available.", false);

        var unsafeEvidence = recommendedCapacity.State == RouteCapacityState.Unknown
            || recommended.TrustEvidence?.Level == TrustLevel.Restricted;
        return Result(unsafeEvidence ? ModelRoutingDisposition.OverrideRequired : ModelRoutingDisposition.Wait,
            recommended, null,
            unsafeEvidence ? ModelRouteSelectionSource.OverrideRequired : ModelRouteSelectionSource.WaitForSafeRoute,
            $"No safe route is available. Recommended route: {recommendedCapacity.Reason} Equivalent fallback and one-tier downgrade did not qualify.", false);

        ModelRoutingResult Result(
            ModelRoutingDisposition disposition,
            ModelRoutingSelectedRoute recommendation,
            ModelRoutingSelectedRoute? selected,
            ModelRouteSelectionSource source,
            string reason,
            bool pinBelowPolicy)
            => new()
            {
                Disposition = disposition,
                TaskKey = request.Task.TaskKey,
                PolicyVersion = _policy.PolicyVersion,
                ModelKnowledgeSchemaVersion = _knowledge.SchemaVersion,
                ModelKnowledgeEvidenceVersion = _knowledge.EvidenceAsOfDate.ToString("yyyy-MM-dd"),
                BenchmarkEvidenceVersion = request.BenchmarkQualification?.EvidenceVersion ?? "unknown",
                BenchmarkGateVersion = request.BenchmarkQualification?.ConfidenceGates.Version,
                WorkflowRole = request.Workflow.Role,
                RecommendedRoute = recommendation,
                SelectedRoute = selected,
                ScoreWorksheet = worksheet,
                CorrectnessFloor = floor,
                SelectionSource = source,
                PolicyReason = policyDecision.Reason,
                FallbackOrWaitReason = reason,
                Uncertainty = new()
                {
                    UpfrontEstimateConfidence = request.UpfrontEstimate.Confidence,
                    Reasons = uncertainties.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                },
                OperatorPin = request.OperatorPin,
                OperatorPinBelowPolicy = pinBelowPolicy,
            };
    }

    private ModelRoutingDecision Recommendation(ModelRoutingSelectionRequest request, ModelRoutingScoreWorksheet worksheet)
    {
        if (request.Workflow.Role == RoutingWorkflowRole.CoreTask)
            return _policy.RecommendCore(new()
            {
                Scorecard = worksheet.Scorecard,
                CorrectnessTriggers = request.UpfrontEstimate.HardFloors
                    .Select(FloorTrigger).Where(trigger => trigger is not null).Select(trigger => trigger!).ToArray(),
                PreviousOutcome = request.PreviousOutcome,
                SemanticFailuresAtStrongerTier = request.SemanticFailuresAtStrongerTier,
            });

        var workflow = request.Workflow;
        if (!workflow.EvidenceIsCompactAndStructured || !workflow.HasDeterministicOutputContract
            || workflow.EvidenceIsUnbounded)
        {
            var route = _knowledge.FindRoute(_knowledge.WorkflowConstraints.AmbiguousOrUnboundedAuthorizingDecisionMinimumRouteId)!;
            return new()
            {
                PolicyVersion = _policy.PolicyVersion,
                Route = route,
                Score = worksheet.Total,
                AppliedHardFloors = ["workflowCapabilityConstraint"],
                ReissuePromoted = false,
                RequiresHumanDecision = false,
                Reason = "Mini/high is disallowed because bounded compact structured evidence and a deterministic output contract were not both established.",
            };
        }

        return _policy.RecommendBoundedDecision(new()
        {
            EvidenceIsAmbiguous = workflow.EvidenceIsAmbiguous,
            EvidenceIsUnbounded = workflow.EvidenceIsUnbounded,
            AuthorizingTriggers = workflow.AuthorizingTriggers,
        });
    }

    private ModelRoutingTier CorrectnessFloor(ModelRoutingDecision decision, RoutingWorkflowRole role)
    {
        if (role == RoutingWorkflowRole.BoundedPipelineDecision)
            return decision.AppliedHardFloors.Count > 0
                ? decision.Route
                : _knowledge.FindRoute(_knowledge.WorkflowConstraints.BoundedPipelineDefaultRouteId)!;
        if (decision.ReissuePromoted) return decision.Route;
        if (decision.AppliedHardFloors.Count == 0) return _coreRoutes[0];
        return _knowledge.HardFloors.Where(floor => decision.AppliedHardFloors.Contains(floor.Id, StringComparer.Ordinal))
            .Select(floor => _knowledge.FindRoute(floor.MinimumRouteId)!)
            .Append(decision.AppliedHardFloors.Contains("workflowCapabilityConstraint", StringComparer.Ordinal) ? decision.Route : _coreRoutes[0])
            .OrderByDescending(route => route.Rank).First();
    }

    private ModelRoutingSelectedRoute? EquivalentFallback(
        ModelRoutingTier recommended,
        ModelRoutingSelectedRoute recommendedCandidate,
        RouteCapacity recommendedCapacity,
        TaskClass taskClass,
        ModelRoutingSelectionRequest request,
        IReadOnlyDictionary<string, ModelTrustAssessment> trust,
        List<string> uncertainties)
    {
        var candidates = new List<ModelRoutingSelectedRoute>();
        foreach (var fallback in _knowledge.FallbacksFor(recommended.Id))
        {
            var resolution = _knowledge.Resolve(fallback.ModelId, fallback.ThinkingLevel, RoutingWorkflowRole.CoreTask);
            if (!resolution.IsResolved) continue;
            var candidate = Candidate(fallback.Id, resolution.Model!, resolution.ThinkingLevel!.Id,
                RoutingWorkflowRole.CoreTask, taskClass, request, trust, fallback.EvidenceStatus, fallback.Provisional);
            if (candidate.BenchmarkQualification is null or { Level: RoutingQualificationLevel.Unknown })
            {
                uncertainties.Add($"Fallback {candidate.RouteId} lacks a usable benchmark qualification for this task.");
                continue;
            }
            if (candidate.TrustEvidence?.Level == TrustLevel.Restricted)
            {
                uncertainties.Add($"Fallback {candidate.RouteId} is restricted by trust evidence.");
                continue;
            }
            var capacity = CapacityOf(candidate, request);
            if (capacity.State != RouteCapacityState.Comfortable)
            {
                uncertainties.Add($"Fallback {candidate.RouteId} is not comfortably available: {capacity.Reason}");
                continue;
            }
            if (recommendedCapacity.State == RouteCapacityState.Comfortable
                && request.Capacity.BudgetPressure == BudgetPressure.Critical
                && (candidate.EfficiencySuggestion.CostClass == CostClass.Unknown
                    || recommendedCandidate.EfficiencySuggestion.CostClass == CostClass.Unknown
                    || candidate.EfficiencySuggestion.CostClass >= recommendedCandidate.EfficiencySuggestion.CostClass))
            {
                uncertainties.Add($"Fallback {candidate.RouteId} is not demonstrably cheaper than the recommended route.");
                continue;
            }
            candidates.Add(candidate);
        }

        var selected = candidates.OrderBy(candidate => candidate.EfficiencySuggestion.CostClass == CostClass.Unknown ? 1 : 0)
            .ThenBy(candidate => candidate.EfficiencySuggestion.CostClass)
            .ThenBy(candidate => candidate.RouteId, StringComparer.Ordinal).FirstOrDefault();
        if (selected is not null) AddUncertainty(selected, request, uncertainties);
        return selected;
    }

    private ModelRoutingSelectedRoute? OneTierDowngrade(
        ModelRoutingDecision decision,
        ModelRoutingSelectedRoute recommended,
        TaskClass taskClass,
        ModelRoutingSelectionRequest request,
        IReadOnlyDictionary<string, ModelTrustAssessment> trust,
        List<string> uncertainties)
    {
        if (request.Workflow.Role != RoutingWorkflowRole.CoreTask
            || decision.AppliedHardFloors.Count > 0
            || decision.ReissuePromoted
            || !request.Capacity.DeterministicVerificationAvailable
            || decision.Route.Rank == 0
            || decision.Route.MinimumScore is not { } threshold
            || decision.Score < threshold
            || decision.Score > threshold + 5)
            return null;

        var lower = _coreRoutes.Single(route => route.Rank == decision.Route.Rank - 1);
        var candidate = Candidate(lower.Id, lower, taskClass, request, trust);
        if (candidate.TrustEvidence?.Level == TrustLevel.Restricted) return null;
        var capacity = CapacityOf(candidate, request);
        var quotaDriven = CapacityOf(recommended, request).State == RouteCapacityState.NearingCap;
        var costDriven = request.Capacity.BudgetPressure == BudgetPressure.Critical
            && candidate.EfficiencySuggestion.CostClass != CostClass.Unknown
            && recommended.EfficiencySuggestion.CostClass != CostClass.Unknown
            && candidate.EfficiencySuggestion.CostClass < recommended.EfficiencySuggestion.CostClass;
        if ((!quotaDriven && !costDriven) || capacity.State is RouteCapacityState.Capped or RouteCapacityState.Unknown or RouteCapacityState.Unavailable)
            return null;
        AddUncertainty(candidate, request, uncertainties);
        return candidate;
    }

    private ModelRoutingSelectedRoute Candidate(
        string routeId,
        ModelRoutingTier tier,
        TaskClass taskClass,
        ModelRoutingSelectionRequest request,
        IReadOnlyDictionary<string, ModelTrustAssessment> trust)
    {
        var model = _knowledge.FindModel(tier.ModelId)!;
        return Candidate(routeId, model, tier.ThinkingLevel, request.Workflow.Role, taskClass, request, trust,
            tier.EvidenceStatus, model.Provisional, tier.Rank);
    }

    private ModelRoutingSelectedRoute Candidate(
        string routeId,
        ModelRoutingModel model,
        string thinkingLevel,
        RoutingWorkflowRole workflowRole,
        TaskClass taskClass,
        ModelRoutingSelectionRequest request,
        IReadOnlyDictionary<string, ModelTrustAssessment> trust,
        PolicyEvidenceStatus? evidenceStatus = null,
        bool? provisional = null,
        int? policyRank = null)
    {
        var suggestion = _matrix.EvaluateModel(model.CanonicalId, taskClass,
            request.Capacity.BudgetPressure, request.Capacity.ProviderAvailability.DecisionAtUtc,
            EffortOf(thinkingLevel))
            ?? throw new InvalidOperationException($"Routing model '{model.CanonicalId}' has no efficiency profile.");
        return new()
        {
            RouteId = routeId,
            ModelId = model.CanonicalId,
            ThinkingLevel = thinkingLevel,
            Cli = CliOf(model),
            WorkflowRole = workflowRole,
            PolicyRank = policyRank ?? RankOf(model, thinkingLevel),
            PolicyEvidenceStatus = evidenceStatus ?? model.EvidenceStatus,
            Provisional = provisional ?? model.Provisional,
            EfficiencySuggestion = suggestion,
            BenchmarkQualification = QualificationFor(model.CanonicalId, thinkingLevel, request),
            TrustEvidence = trust.GetValueOrDefault(model.CanonicalId),
        };
    }

    private RouteCapacity CapacityOf(ModelRoutingSelectedRoute route, ModelRoutingSelectionRequest request)
    {
        if (!request.AvailableClis.Contains(route.Cli))
            return new(RouteCapacityState.Unavailable, $"{route.Cli} is not in the available CLI set.");
        var model = _knowledge.FindModel(route.ModelId)!;
        var provider = request.Capacity.ProviderAvailability.Providers.SingleOrDefault(row =>
            string.Equals(row.Provider, model.ProviderId, StringComparison.OrdinalIgnoreCase)
            && CliMatches(row.CliType, route.Cli));
        if (provider is null)
            return new(RouteCapacityState.Unknown, $"No provider availability row exists for {model.ProviderId}/{route.Cli}.");
        if (provider.Availability == ProviderCliAvailability.Unavailable)
            return new(RouteCapacityState.Capped, provider.AvailabilityDetail ?? "The provider CLI is unavailable.");
        if (provider.Availability != ProviderCliAvailability.Available || provider.Freshness != SnapshotFreshness.Fresh)
            return new(RouteCapacityState.Unknown, "Provider availability is unknown or stale.");
        if (provider.QuotaWindows.Count == 0 || provider.QuotaWindows.Any(window =>
                window.Freshness != SnapshotFreshness.Fresh || window.WarningState == AvailabilityWarningState.Unknown))
            return new(RouteCapacityState.Unknown, "Quota evidence is missing, stale, suspicious, or unknown.");
        if (provider.QuotaWindows.Any(window => window.WarningState == AvailabilityWarningState.Critical))
            return new(RouteCapacityState.Capped, "At least one provider quota window is critical or exhausted.");
        if (provider.QuotaWindows.Any(window => window.WarningState == AvailabilityWarningState.Warning))
            return new(RouteCapacityState.NearingCap, "At least one provider quota window is nearing its cap.");
        return new(RouteCapacityState.Comfortable, "Provider and quota evidence are fresh with comfortable headroom.");
    }

    private RoutingQualification? QualificationFor(string model, string thinking, ModelRoutingSelectionRequest request)
    {
        if (request.BenchmarkQualification is not { } report) return null;
        var taskClass = EvidenceTaskClass(request.Task.TaskType);
        if (taskClass is null) return null;
        var capability = string.IsNullOrWhiteSpace(request.RequiredBenchmarkCapability)
            ? null : request.RequiredBenchmarkCapability.Trim().ToLowerInvariant();
        var matches = report.ControlledCohorts.Select(cohort => (Cohort: cohort, Controlled: true))
            .Concat(report.ObservationalCohorts.Select(cohort => (Cohort: cohort, Controlled: false)))
            .Where(item => string.Equals(item.Cohort.CanonicalModel, model, StringComparison.Ordinal)
                && string.Equals(item.Cohort.ThinkingLevel, thinking, StringComparison.Ordinal)
                && (taskClass is null || string.Equals(item.Cohort.TaskClass, taskClass, StringComparison.Ordinal))
                && (capability is null || string.Equals(item.Cohort.Capability, capability, StringComparison.Ordinal)))
            .ToArray();
        if (capability is null && matches.Select(item => item.Cohort.Capability).Distinct(StringComparer.Ordinal).Count() != 1)
            return null;
        return matches.OrderByDescending(item => item.Cohort.Qualification.Level)
            .ThenByDescending(item => item.Controlled)
            .ThenBy(item => item.Cohort.Capability, StringComparer.Ordinal)
            .Select(item => item.Cohort.Qualification).FirstOrDefault();
    }

    private bool MeetsRecommendation(ModelRoutingSelectedRoute candidate, ModelRoutingTier recommended)
    {
        if (_knowledge.ProviderFallbacks.Any(fallback => fallback.ModelId == candidate.ModelId
            && fallback.ThinkingLevel == candidate.ThinkingLevel
            && fallback.ForRouteIds.Contains(recommended.Id, StringComparer.Ordinal)))
            return true;
        var model = _knowledge.FindModel(candidate.ModelId)!;
        var expectedModel = _knowledge.FindModel(recommended.ModelId)!;
        var thinking = _knowledge.FindThinkingLevel(candidate.ThinkingLevel)!;
        var expectedThinking = _knowledge.FindThinkingLevel(recommended.ThinkingLevel)!;
        return model.CapabilityTier >= expectedModel.CapabilityTier && thinking.Rank >= expectedThinking.Rank;
    }

    private void AddUncertainty(
        ModelRoutingSelectedRoute route,
        ModelRoutingSelectionRequest request,
        List<string> uncertainties)
    {
        if (route.Provisional)
            uncertainties.Add($"Route {route.RouteId} is provisional in policy {_policy.PolicyVersion}.");
        if (route.BenchmarkQualification is null)
            uncertainties.Add($"No matching benchmark qualification was supplied for {route.ModelId}/{route.ThinkingLevel}.");
        else if (route.BenchmarkQualification.Level != RoutingQualificationLevel.Validated)
            uncertainties.Add($"Benchmark qualification for {route.ModelId}/{route.ThinkingLevel} is {route.BenchmarkQualification.Level}.");
        if (route.TrustEvidence is null)
            uncertainties.Add($"Named-model trust evidence is {_knowledge.CatalogContracts.NamedModelTrustEvidenceStatus} for {route.ModelId}.");
        else if (route.TrustEvidence.Level != TrustLevel.Verified)
            uncertainties.Add($"Trust evidence for {route.ModelId} is {route.TrustEvidence.Level}.");
        if (route.EfficiencySuggestion.CostClass == CostClass.Unknown)
            uncertainties.Add($"Cost is unknown for {route.ModelId} at {request.Capacity.ProviderAvailability.DecisionAtUtc:O}.");
        else if (route.EfficiencySuggestion.CostUnconfirmed)
            uncertainties.Add($"Cost is unconfirmed for {route.ModelId} at {request.Capacity.ProviderAvailability.DecisionAtUtc:O}.");
    }

    private ModelRoutingScoreWorksheet Worksheet(TaskComplexityEstimate estimate)
    {
        var items = new[]
        {
            Item("correctnessRisk", estimate.CorrectnessRisk),
            Item("expectedScope", estimate.ExpectedScope),
            Item("contextDemand", estimate.ContextDemand),
            Item("taskTypeAndUncertainty", estimate.TaskUncertainty),
            Item("empiricalConfidence", estimate.EmpiricalConfidence),
            Item("quotaAndCostHeadroom", estimate.QuotaAndCostHeadroom),
        };
        var scorecard = new ModelRoutingScorecard
        {
            CorrectnessRisk = items[0].Score,
            ExpectedScope = items[1].Score,
            ContextDemand = items[2].Score,
            TaskTypeAndUncertainty = items[3].Score,
            EmpiricalConfidence = items[4].Score,
            QuotaAndCostHeadroom = items[5].Score,
        };
        var policyMaximums = _knowledge.ScoringCriteria.ToDictionary(item => item.Id, item => item.MaximumPoints, StringComparer.Ordinal);
        foreach (var item in items)
            if (!policyMaximums.TryGetValue(item.Id, out var maximum) || item.MaximumScore != maximum)
                throw new ArgumentException($"The upfront estimate maximum for {item.Id} does not match policy {_policy.PolicyVersion}.", nameof(estimate));
        if (Math.Abs(scorecard.Total - estimate.Score) > .001)
            throw new ArgumentException("The upfront estimate total does not equal its six score criteria.", nameof(estimate));
        return new()
        {
            Scorecard = scorecard,
            Criteria = items,
            Total = scorecard.Total,
            EffectivePolicyScore = scorecard.Total,
            Evidence = estimate.ScoreEvidence,
        };

        static ModelRoutingScoreItem Item(string id, ComplexityRoutingCriterion criterion)
        {
            var score = Integer(criterion.Score, $"{id} score");
            var maximum = Integer(criterion.MaximumScore, $"{id} maximum");
            if (score < 0 || score > maximum)
                throw new ArgumentOutOfRangeException(nameof(estimate), $"{id} must be between 0 and {maximum}.");
            return new(id, score, maximum, criterion.Evidence);
        }

        static int Integer(double value, string label)
        {
            var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (Math.Abs(value - integer) > .001) throw new ArgumentException($"{label} must be an integer policy point value.", nameof(estimate));
            return integer;
        }
    }

    private static IReadOnlyDictionary<string, ModelTrustAssessment> TrustByModel(IEnumerable<ModelTrustAssessment> evidence)
    {
        var result = new Dictionary<string, ModelTrustAssessment>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in evidence)
            if (!result.TryAdd(item.ModelId, item))
                throw new ArgumentException($"Duplicate trust assessment for model '{item.ModelId}'.", nameof(evidence));
        return result;
    }

    private static void Validate(ModelRoutingSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Task);
        ArgumentNullException.ThrowIfNull(request.UpfrontEstimate);
        ArgumentNullException.ThrowIfNull(request.Capacity);
        ArgumentNullException.ThrowIfNull(request.Capacity.ProviderAvailability);
        ArgumentNullException.ThrowIfNull(request.Workflow);
        if (!string.Equals(request.Task.TaskKey, request.UpfrontEstimate.TaskKey, StringComparison.Ordinal))
            throw new ArgumentException("Task and upfront estimate keys must match.", nameof(request));
        if (request.UpfrontEstimate.SchemaVersion != TaskComplexityEstimate.CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported upfront estimate schema version {request.UpfrontEstimate.SchemaVersion}.", nameof(request));
        if (request.BenchmarkQualification is { } report
            && (report.SchemaVersion != RoutingEvidenceReport.CurrentSchemaVersion
                || report.ConfidenceGates.Version != RoutingEvidenceConfidenceGates.CurrentVersion))
            throw new ArgumentException("Unsupported benchmark qualification or confidence-gate version.", nameof(request));
        if (request.Capacity.ProviderAvailability.DecisionAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Capacity decision time must be UTC.", nameof(request));
        if (request.SemanticFailuresAtStrongerTier < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Semantic failure count cannot be negative.");
    }

    private static string? FloorTrigger(ComplexityHardFloor floor) => floor.Trigger switch
    {
        ComplexityHardFloorTrigger.P0 => "p0",
        ComplexityHardFloorTrigger.Fencing => "fencing",
        ComplexityHardFloorTrigger.LeaseOwnership => "leaseOwnership",
        ComplexityHardFloorTrigger.StaleWriteRejection => "staleWriteRejection",
        ComplexityHardFloorTrigger.DistributedAuthority => "distributedAuthority",
        ComplexityHardFloorTrigger.SecurityBoundary => "securityBoundary",
        ComplexityHardFloorTrigger.CredibleDataLoss => "credibleDataLoss",
        ComplexityHardFloorTrigger.PublicProtocol => "publicProtocol",
        ComplexityHardFloorTrigger.PersistentStateMigration => "persistentStateMigration",
        ComplexityHardFloorTrigger.ThreeOrMoreRuntimeSubsystems => "threeOrMoreRuntimeSubsystems",
        ComplexityHardFloorTrigger.UnclearBug => "unclearBug",
        _ => null,
    };

    private int RankOf(ModelRoutingModel model, string thinking)
    {
        var exact = _coreRoutes.SingleOrDefault(route => route.ModelId == model.CanonicalId && route.ThinkingLevel == thinking);
        if (exact is not null) return exact.Rank;
        var equivalent = _knowledge.ProviderFallbacks.FirstOrDefault(fallback =>
            fallback.ModelId == model.CanonicalId && fallback.ThinkingLevel == thinking);
        if (equivalent is not null)
            return equivalent.ForRouteIds.Select(id => _knowledge.FindRoute(id)!.Rank).DefaultIfEmpty(0).Max();
        return 0;
    }

    private static Cli CliOf(ModelRoutingModel model) => model.CliId switch
    {
        "codex" => Cli.Codex,
        "claude-code" => Cli.Claude,
        _ => throw new InvalidOperationException($"Unknown policy CLI '{model.CliId}'."),
    };

    private static EffortLevel EffortOf(string thinkingLevel)
        => Enum.TryParse<EffortLevel>(thinkingLevel, true, out var effort)
            ? effort
            : throw new InvalidOperationException($"Unknown route thinking level '{thinkingLevel}'.");

    private static bool CliMatches(string value, Cli cli) => value.Trim().ToLowerInvariant() switch
    {
        "codex" => cli == Cli.Codex,
        "claude" or "claude-code" => cli == Cli.Claude,
        _ => false,
    };

    private static TaskClass TaskClassOf(string? value) => EvidenceTaskClass(value) switch
    {
        "heavy-design" => TaskClass.HeavyDesign,
        "mechanical-chore" => TaskClass.MechanicalChore,
        "doc-edit" => TaskClass.DocEdit,
        "research" => TaskClass.Research,
        _ => TaskClass.Feature,
    };

    private static string? EvidenceTaskClass(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "feature" or "bug" or "bugfix" or "bug-fix" => "feature",
        "chore" or "mechanical" or "mechanical-chore" => "mechanical-chore",
        "doc" or "docs" or "documentation" or "doc-edit" => "doc-edit",
        "research" or "analysis" or "investigation" => "research",
        "design" or "architecture" or "heavy-design" => "heavy-design",
        _ => null,
    };

    private enum RouteCapacityState { Comfortable, NearingCap, Capped, Unknown, Unavailable }
    private sealed record RouteCapacity(RouteCapacityState State, string Reason);
}

#pragma warning restore CS1591
