namespace TokenEconomy;

#pragma warning disable CS1591 // Public members mirror the documented policy request and result fields.

/// <summary>The result type of a previous run when applying policy reissue rules.</summary>
public enum RoutingAttemptOutcome
{
    None,
    SemanticFailure,
    SubstantiveLowGrade,
    EnvironmentalFailure,
    StaleBase,
    BrokenTestHost,
    Cancellation,
    QuotaTruncation,
    MissingDeliveryPath,
}

/// <summary>A validated six-axis scorecard from the authoritative weighted decision.</summary>
public sealed record ModelRoutingScorecard
{
    public required int CorrectnessRisk { get; init; }
    public required int ExpectedScope { get; init; }
    public required int ContextDemand { get; init; }
    public required int TaskTypeAndUncertainty { get; init; }
    public required int EmpiricalConfidence { get; init; }
    public required int QuotaAndCostHeadroom { get; init; }
    public int Total => CorrectnessRisk + ExpectedScope + ContextDemand + TaskTypeAndUncertainty + EmpiricalConfidence + QuotaAndCostHeadroom;
}

/// <summary>Inputs that may raise a core route after scoring.</summary>
public sealed record ModelRoutingRequest
{
    public required ModelRoutingScorecard Scorecard { get; init; }
    public IReadOnlyList<string> CorrectnessTriggers { get; init; } = [];
    public RoutingAttemptOutcome PreviousOutcome { get; init; }
    public int SemanticFailuresAtStrongerTier { get; init; }
}

/// <summary>Inputs for the Mini role exception and its authorizing-decision floor.</summary>
public sealed record BoundedDecisionRoutingRequest
{
    public bool EvidenceIsAmbiguous { get; init; }
    public bool EvidenceIsUnbounded { get; init; }
    public IReadOnlyList<string> AuthorizingTriggers { get; init; } = [];
}

/// <summary>An auditable routing result independent of price catalog and quota availability.</summary>
public sealed record ModelRoutingDecision
{
    public required string PolicyVersion { get; init; }
    public required ModelRoutingTier Route { get; init; }
    public required int Score { get; init; }
    /// <summary>The empirical-uncertainty points after applying the previous-attempt outcome.</summary>
    public required int EffectiveEmpiricalConfidence { get; init; }
    public required IReadOnlyList<string> AppliedHardFloors { get; init; }
    public required bool ReissuePromoted { get; init; }
    public required bool RequiresHumanDecision { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// Pure evaluator for the authoritative score tiers, correctness floors, bounded-decision exception,
/// and reissue rule. It accepts no price, cost class, or quota snapshot, so those data cannot redefine
/// a correctness floor. Quota/cost headroom is only the policy's bounded 0-5 intake subscore.
/// </summary>
public sealed class ModelRoutingPolicy
{
    private readonly ModelRoutingKnowledgeBase _knowledge;
    private readonly IReadOnlyList<ModelRoutingTier> _coreRoutes;

    public ModelRoutingPolicy(ModelRoutingKnowledgeBase knowledge)
    {
        _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
        _coreRoutes = knowledge.Routes.Where(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask)
            .OrderBy(route => route.Rank).ToArray();
    }

    public static ModelRoutingPolicy Default { get; } = new(ModelRoutingKnowledgeBase.Default);

    /// <summary>The exact versioned knowledge used by this evaluator.</summary>
    public ModelRoutingKnowledgeBase Knowledge => _knowledge;

    /// <summary>The canonical policy version used by this evaluator.</summary>
    public string PolicyVersion => _knowledge.PolicyVersion.ToString("yyyy-MM-dd");

    /// <summary>Apply the weighted ladder, then hard floors, then semantic-reissue promotion.</summary>
    public ModelRoutingDecision RecommendCore(ModelRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scorecard);
        ValidateScorecard(request.Scorecard);
        ValidateKnownTriggers(request.CorrectnessTriggers, _knowledge.HardFloors.SelectMany(floor => floor.Triggers), nameof(request.CorrectnessTriggers));
        if (request.SemanticFailuresAtStrongerTier < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Semantic failure count cannot be negative.");

        var baseRoute = _coreRoutes.Single(candidate => request.Scorecard.Total >= candidate.MinimumScore && request.Scorecard.Total <= candidate.MaximumScore);
        var semanticReissue = request.PreviousOutcome is RoutingAttemptOutcome.SemanticFailure or RoutingAttemptOutcome.SubstantiveLowGrade;
        var effectiveScore = semanticReissue
            ? request.Scorecard.Total - request.Scorecard.EmpiricalConfidence + _knowledge.ReissueRules.EmpiricalConfidencePointsAfterSemanticFailure
            : request.Scorecard.Total;
        var route = _coreRoutes.Single(candidate => effectiveScore >= candidate.MinimumScore && effectiveScore <= candidate.MaximumScore);
        var floorIds = new List<string>();
        foreach (var floor in _knowledge.HardFloors)
        {
            if (!floor.Triggers.Intersect(request.CorrectnessTriggers, StringComparer.Ordinal).Any()) continue;
            floorIds.Add(floor.Id);
            var floorRoute = _knowledge.FindRoute(floor.MinimumRouteId)!;
            if (floorRoute.Rank > route.Rank) route = floorRoute;
        }

        var promoted = false;
        var requiresHuman = semanticReissue && request.SemanticFailuresAtStrongerTier >= _knowledge.ReissueRules.StopAfterSemanticFailuresAtStrongerTier;
        if (semanticReissue && !requiresHuman)
        {
            var minimumPromotedRank = Math.Min(baseRoute.Rank + _knowledge.ReissueRules.MinimumCoreTierIncrease, _coreRoutes.Max(candidate => candidate.Rank));
            var promotedRank = Math.Max(route.Rank, minimumPromotedRank);
            promoted = promotedRank > baseRoute.Rank;
            route = _coreRoutes.Single(candidate => candidate.Rank == promotedRank);
        }

        var reason = requiresHuman
            ? "Two semantic failures at the stronger tier require narrower scope, better evidence, or a human decision; model escalation stops."
            : $"Score {effectiveScore} maps to the core ladder; {floorIds.Count} hard floor(s) applied; semantic promotion {(promoted ? "applied" : "not applied")}.";
        return Decision(route, effectiveScore,
            semanticReissue ? _knowledge.ReissueRules.EmpiricalConfidencePointsAfterSemanticFailure : request.Scorecard.EmpiricalConfidence,
            floorIds, promoted, requiresHuman, reason);
    }

    /// <summary>Choose Mini/high only for a bounded decision; ambiguous or unbounded authorizing evidence raises the route to Sol/medium.</summary>
    public ModelRoutingDecision RecommendBoundedDecision(BoundedDecisionRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKnownTriggers(request.AuthorizingTriggers, _knowledge.WorkflowConstraints.AuthorizingDecisionTriggers, nameof(request.AuthorizingTriggers));
        var isAuthorizing = request.AuthorizingTriggers.Intersect(_knowledge.WorkflowConstraints.AuthorizingDecisionTriggers, StringComparer.Ordinal).Any();
        var raised = isAuthorizing && (request.EvidenceIsAmbiguous || request.EvidenceIsUnbounded);
        var routeId = raised
            ? _knowledge.WorkflowConstraints.AmbiguousOrUnboundedAuthorizingDecisionMinimumRouteId
            : _knowledge.WorkflowConstraints.BoundedPipelineDefaultRouteId;
        var route = _knowledge.FindRoute(routeId)!;
        var floors = raised ? new[] { "ambiguousOrUnboundedAuthorizingDecision" } : [];
        var reason = raised
            ? "Ambiguous or unbounded evidence can authorize a consequential action, so Mini is below the workflow floor."
            : "Compact bounded evidence with a deterministic output contract uses the Mini role exception.";
        return Decision(route, 0, 0, floors, false, false, reason);
    }

    private ModelRoutingDecision Decision(
        ModelRoutingTier route,
        int score,
        int effectiveEmpiricalConfidence,
        IReadOnlyList<string> floors,
        bool promoted,
        bool requiresHuman,
        string reason)
        => new()
        {
            PolicyVersion = _knowledge.PolicyVersion.ToString("yyyy-MM-dd"),
            Route = route,
            Score = score,
            EffectiveEmpiricalConfidence = effectiveEmpiricalConfidence,
            AppliedHardFloors = floors,
            ReissuePromoted = promoted,
            RequiresHumanDecision = requiresHuman,
            Reason = reason,
        };

    private void ValidateScorecard(ModelRoutingScorecard scorecard)
    {
        var maximumById = _knowledge.ScoringCriteria.ToDictionary(criterion => criterion.Id, criterion => criterion.MaximumPoints, StringComparer.Ordinal);
        InRange(scorecard.CorrectnessRisk, maximumById["correctnessRisk"], nameof(scorecard.CorrectnessRisk));
        InRange(scorecard.ExpectedScope, maximumById["expectedScope"], nameof(scorecard.ExpectedScope));
        InRange(scorecard.ContextDemand, maximumById["contextDemand"], nameof(scorecard.ContextDemand));
        InRange(scorecard.TaskTypeAndUncertainty, maximumById["taskTypeAndUncertainty"], nameof(scorecard.TaskTypeAndUncertainty));
        InRange(scorecard.EmpiricalConfidence, maximumById["empiricalConfidence"], nameof(scorecard.EmpiricalConfidence));
        InRange(scorecard.QuotaAndCostHeadroom, maximumById["quotaAndCostHeadroom"], nameof(scorecard.QuotaAndCostHeadroom));
    }

    private static void InRange(int value, int maximum, string name)
    {
        if (value < 0 || value > maximum)
            throw new ArgumentOutOfRangeException(name, value, $"Policy points must be between 0 and {maximum}.");
    }

    private static void ValidateKnownTriggers(IEnumerable<string> requested, IEnumerable<string> known, string name)
    {
        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var unknown = requested.Where(trigger => !knownSet.Contains(trigger)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException($"Unknown policy trigger(s): {string.Join(", ", unknown)}.", name);
    }
}

#pragma warning restore CS1591
