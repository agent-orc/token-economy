using System.Globalization;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Attempt-attributed weekly probes. A reset or concurrent account activity makes the delta unknown.</summary>
public sealed record RunQuotaMeasurement(
    string SubscriptionId, string WindowId, DateTime WindowStartedAtUtc, DateTime ResetsAtUtc,
    DateTime BeforeAtUtc, DateTime AfterAtUtc, decimal BeforeUsedPercent, decimal AfterUsedPercent,
    bool ExclusiveAttribution)
{
    public decimal? Share => ExclusiveAttribution && !string.IsNullOrWhiteSpace(SubscriptionId)
        && !string.IsNullOrWhiteSpace(WindowId)
        && ResetsAtUtc - WindowStartedAtUtc == TimeSpan.FromDays(7)
        && BeforeAtUtc >= WindowStartedAtUtc && AfterAtUtc < ResetsAtUtc && AfterAtUtc >= BeforeAtUtc
        && BeforeUsedPercent is >= 0 and <= 100 && AfterUsedPercent >= BeforeUsedPercent && AfterUsedPercent <= 100
        ? AfterUsedPercent - BeforeUsedPercent : null;
}

public sealed record CardEconomicsCandidate(string Model, string ThinkingLevel);
public enum CardEconomicsCurrency { Usd, WeeklyQuotaPercent }
public sealed record CardEconomicsQuery
{
    public required string OrganisationId { get; init; }
    public required string TaskClass { get; init; }
    public ComplexityCard? Card { get; init; }
    public required IReadOnlyList<CardEconomicsCandidate> Candidates { get; init; }
    public DateTime SinceUtc { get; init; } = new(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc);
    public required DateTime AsOfUtc { get; init; }
    public CardEconomicsCurrency Currency { get; init; }
    public string? SubscriptionId { get; init; }
    public bool ExcludeObservedAccessRefusals { get; init; }
}
public sealed record ExpectedRunTokens(decimal UncachedInput, decimal CacheRead, decimal CacheWrite, decimal Output);
public sealed record OrganisationErrorFact(string ErrorClass, int Count, int ObservedRuns, decimal Rate, DateTime LastSeenUtc, string? Detail);
public sealed record CardEconomicsRow
{
    public required CardEconomicsCandidate Candidate { get; init; }
    public int Rank { get; init; }
    public required bool Eligible { get; init; }
    public required string EligibilityReason { get; init; }
    public bool Selectable { get; init; }
    public bool Provisional { get; init; }
    public int Cards { get; init; }
    public int CompletedCards { get; init; }
    public int ExcludedCards { get; init; }
    public int Runs { get; init; }
    public decimal? CompletionProbability { get; init; }
    public decimal? ExpectedRoundsPerCompletedCard { get; init; }
    public ExpectedRunTokens? TokensPerCompletedCard { get; init; }
    public decimal? ExpectedUsdPerCompletedCard { get; init; }
    public bool UnconfirmedPrices { get; init; }
    public string CostCaveat { get; init; } = ModelPrice.EstimatedListPricesCaveat;
    public decimal? ExpectedWeeklyQuotaPercentPerCompletedCard { get; init; }
    public decimal? ExpectedDurationSeconds { get; init; }
    public int KnownReviews { get; init; }
    public int FavorableReviews { get; init; }
    public required string Confidence { get; init; }
    public int AvailabilityTotalRuns { get; init; }
    public int AvailabilityObservedRuns { get; init; }
    public required string Availability { get; init; }
    public IReadOnlyList<OrganisationErrorFact> Errors { get; init; } = [];
    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}
public sealed record CardEconomicsDecision(CardEconomicsQuery Query, string PolicyVersion,
    IReadOnlyList<CardEconomicsRow> Rows, string Recommendation);

/// <summary>Observational, same-route card cohorts; never changes policy defaults or fallbacks.</summary>
public sealed class CardEconomics(ModelPriceCatalog? prices = null, ModelRoutingKnowledgeBase? knowledge = null)
{
    private readonly ModelPriceCatalog _prices = prices ?? ModelPriceCatalog.Default;
    private readonly ModelRoutingKnowledgeBase _knowledge = knowledge ?? ModelRoutingKnowledgeBase.Default;

    public CardEconomicsDecision Decide(CardEconomicsQuery query, IEnumerable<AgentStudioRunRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.OrganisationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.TaskClass);
        if (query.SinceUtc.Kind != DateTimeKind.Utc || query.AsOfUtc.Kind != DateTimeKind.Utc || query.SinceUtc > query.AsOfUtc)
            throw new ArgumentException("A valid UTC evidence window is required.", nameof(query));
        if (query.Card?.TaskType is { } taskType && !Same(taskType, query.TaskClass))
            throw new ArgumentException("Card task type must match the queried task class.", nameof(query));
        if (query.Candidates.Count == 0 || !Enum.IsDefined(query.Currency))
            throw new ArgumentException("At least one candidate and a supported currency are required.", nameof(query));
        var all = records.Where(r => Same(r.OrganisationId, query.OrganisationId) && r.ExecutedAtUtc <= query.AsOfUtc && r.ObservedAtUtc <= query.AsOfUtc)
            .GroupBy(r => (r.Project, r.TaskKey, r.Run)).Select(g => g.OrderByDescending(r => r.ObservedAtUtc).First()).ToArray();
        var card = query.Card ?? new ComplexityCard { TaskKey = "economics-query", Prompt = "", TaskType = query.TaskClass };
        var estimate = new TaskComplexityEstimator().Estimate(card);
        var requiredBand = estimate.HardFloors.Select(f => f.MinimumBand).DefaultIfEmpty(TaskComplexityLevel.Trivial).Max();
        var floor = _knowledge.Routes.Where(r => r.WorkflowRole == RoutingWorkflowRole.CoreTask).OrderBy(r => r.Rank).ElementAt((int)requiredBand);
        var rows = query.Candidates.Distinct().Select(candidate => Build(query, candidate, all, floor, estimate.HardFloors.Count > 0)).ToArray();
        decimal? Metric(CardEconomicsRow row) => query.Currency == CardEconomicsCurrency.Usd
            ? row.ExpectedUsdPerCompletedCard : row.ExpectedWeeklyQuotaPercentPerCompletedCard;
        var ranked = rows.OrderByDescending(r => r.Eligible).ThenBy(r => Metric(r) is null)
            .ThenBy(Metric).ThenBy(r => r.Candidate.Model, StringComparer.Ordinal).ThenBy(r => r.Candidate.ThinkingLevel, StringComparer.Ordinal)
            .Select((r, i) => r with { Rank = i + 1 }).ToArray();
        var best = ranked.FirstOrDefault(r => r.Eligible && Metric(r) is not null);
        var recommendation = best is null ? "No evidence-backed cost recommendation is available; retain the policy route and collect complete local card and quota evidence."
            : $"Evaluate {best.Candidate.Model}/{best.Candidate.ThinkingLevel}: lowest observed {query.Currency} per completed card among eligible measured candidates ({best.Confidence}; observational evidence, not a policy change).";
        return new(query, _knowledge.PolicyVersion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ranked, recommendation);
    }

    private CardEconomicsRow Build(CardEconomicsQuery q, CardEconomicsCandidate candidate, AgentStudioRunRecord[] all,
        ModelRoutingTier floor, bool hardFloor)
    {
        var resolution = _knowledge.Resolve(candidate.Model, candidate.ThinkingLevel, RoutingWorkflowRole.CoreTask);
        var model = resolution.Model?.CanonicalId ?? candidate.Model;
        var level = resolution.ThinkingLevel?.Id ?? candidate.ThinkingLevel;
        bool Match(AgentStudioRunRecord r) => Same(_knowledge.FindModel(r.ActualModel)?.CanonicalId ?? r.ActualModel, model) && Same(r.ActualThinkingLevel, level);
        var modelRuns = all.Where(r => r.ExecutedAtUtc >= q.SinceUtc && Same(_knowledge.FindModel(r.ActualModel)?.CanonicalId ?? r.ActualModel, model)).ToArray();
        var observed = modelRuns.Where(r => r.ProviderErrorClass is not null).ToArray();
        var errors = observed.Where(r => !Same(r.ProviderErrorClass, "none")).GroupBy(r => r.ProviderErrorClass!)
            .Select(g => new OrganisationErrorFact(g.Key, g.Count(), observed.Length, (decimal)g.Count() / observed.Length,
                g.Max(r => r.ExecutedAtUtc), g.OrderByDescending(r => r.ExecutedAtUtc).First().ProviderErrorMessage)).ToArray();
        var accessRefusal = errors.Any(e => e.ErrorClass.Contains("access", StringComparison.OrdinalIgnoreCase)
            || e.Detail?.Contains("access_programs", StringComparison.OrdinalIgnoreCase) == true);
        var safe = !hardFloor || _knowledge.Routes.Any(r => r.WorkflowRole == RoutingWorkflowRole.CoreTask && r.Rank >= floor.Rank
            && Same(r.ModelId, model) && _knowledge.FindThinkingLevel(level)?.Rank >= _knowledge.FindThinkingLevel(r.ThinkingLevel)?.Rank)
            || _knowledge.FallbacksFor(floor.Id).Any(f => Same(f.ModelId, model) && Same(f.ThinkingLevel, level));
        var eligible = resolution.IsResolved && safe && !(q.ExcludeObservedAccessRefusals && accessRefusal);
        var groups = all.GroupBy(r => (r.Project, r.TaskKey)).Where(g => g.Any(Match)
            && g.Any(r => r.ExecutedAtUtc >= q.SinceUtc) && Same(g.First().TaskType, q.TaskClass)
            && (q.Card is null || (!Same(g.Key.TaskKey, q.Card.TaskKey)
                && (q.Card.Area is null || Same(g.First().Area, q.Card.Area))
                && (q.Card.ExpectedChangedLines is null || Size(g.First().ExpectedChangedLines) == Size(q.Card.ExpectedChangedLines))
                && (q.Card.ReferencedSubsystems.Count == 0 || q.Card.ReferencedSubsystems.All(s => g.First().ReferencedSubsystems.Contains(s, StringComparer.OrdinalIgnoreCase))))))
            .Select(g => g.OrderBy(r => r.Run).ToArray()).ToArray();
        // Truncated, mixed-route and still-open histories cannot estimate completion probability.
        var cohorts = groups.Where(g => g.All(Match) && g.All(r => r.ExecutedAtUtc >= q.SinceUtc)
            && g[0].Run is 0 or 1 && g.Zip(g.Skip(1)).All(p => p.Second.Run == p.First.Run + 1)
            && g.All(r => r.RouteGranularity == AgentStudioRouteGranularity.Attempt || g.Length == 1 && r.Run <= 1)
            && CardOutcome(g[^1]) is OutcomeQualitySignal.Successful or OutcomeQualitySignal.Unsuccessful).ToArray();
        var attempts = cohorts.SelectMany(g => g).ToArray();
        var completed = cohorts.Count(g => CardOutcome(g[^1]) == OutcomeQualitySignal.Successful);
        var costs = attempts.Select(r => _prices.ComputeCost(r.ActualModel, r.Usage, r.ExecutedAtUtc)).ToArray();
        var tokensKnown = completed > 0 && attempts.All(r => r.TokenUsageAvailable && r.Usage.Input >= 0 && r.Usage.Output >= 0 && r.Usage.CacheRead >= 0 && r.Usage.CacheWrite >= 0);
        var quotaKnown = completed > 0 && q.SubscriptionId is not null && attempts.All(r => r.WeeklyQuota?.SubscriptionId == q.SubscriptionId && r.WeeklyQuota.Share is not null);
        decimal? Seconds(AgentStudioRunRecord r) => r.OutcomeObservation?.DurationMs is >= 0
            ? r.OutcomeObservation.DurationMs.Value / 1000m
            : r.StartedAtUtc is { } start && start <= r.ExecutedAtUtc ? (decimal)(r.ExecutedAtUtc - start).TotalSeconds : null;
        return new()
        {
            Candidate = new(model, level), Eligible = eligible, Selectable = resolution.IsResolved, Provisional = resolution.Model?.Provisional ?? true,
            EligibilityReason = !resolution.IsResolved ? resolution.Reason : !safe ? "No declared equivalence clearing the correctness floor."
                : !eligible ? "Observed organisation access refusal; excluded by query." : "Eligible for evidence comparison; policy defaults unchanged.",
            Cards = cohorts.Length, CompletedCards = completed, ExcludedCards = groups.Length - cohorts.Length, Runs = attempts.Length,
            CompletionProbability = cohorts.Length > 0 ? (decimal)completed / cohorts.Length : null,
            ExpectedRoundsPerCompletedCard = completed > 0 ? (decimal)attempts.Length / completed : null,
            TokensPerCompletedCard = tokensKnown ? new(attempts.Sum(r => (decimal)r.Usage.Input) / completed,
                attempts.Sum(r => (decimal)r.Usage.CacheRead) / completed, attempts.Sum(r => (decimal)r.Usage.CacheWrite) / completed,
                attempts.Sum(r => (decimal)r.Usage.Output) / completed) : null,
            ExpectedUsdPerCompletedCard = tokensKnown && costs.All(c => c.Total is not null && c.Price?.Currency == Currencies.Usd)
                ? costs.Sum(c => c.Total!.Value) / completed : null,
            UnconfirmedPrices = costs.Any(c => c.Unconfirmed),
            ExpectedWeeklyQuotaPercentPerCompletedCard = quotaKnown ? attempts.Sum(r => r.WeeklyQuota!.Share!.Value) / completed : null,
            ExpectedDurationSeconds = completed > 0 && attempts.All(r => Seconds(r) is not null) ? attempts.Sum(r => Seconds(r)!.Value) / completed : null,
            KnownReviews = attempts.Count(r => r.ReviewOutcome != AgentStudioReviewOutcome.Unknown),
            FavorableReviews = attempts.Count(r => r.ReviewOutcome is AgentStudioReviewOutcome.Approved or AgentStudioReviewOutcome.GradeA or AgentStudioReviewOutcome.GradeB),
            Confidence = cohorts.Length == 0 ? "unknown" : cohorts.Length < 20 ? "small observational cohort" : "observational cohort; selection bias remains",
            AvailabilityTotalRuns = modelRuns.Length, AvailabilityObservedRuns = observed.Length,
            Availability = observed.Length == 0 ? "unknown" : errors.Length == 0 ? "observed usable"
                : observed.Length == modelRuns.Length && errors.Sum(e => e.Count) == observed.Length ? "observed unusable; all observed runs errored"
                : "partly usable; observed provider errors",
            Errors = errors,
            EvidenceReferences = attempts.Select(r => r.ProvenanceReference ?? $"{r.Project}/{r.TaskKey}/attempt/{r.Run}").Distinct().ToArray(),
        };
    }

    private static OutcomeQualitySignal CardOutcome(AgentStudioRunRecord run) => run.CardOutcome
        ?? (run.RouteGranularity == AgentStudioRouteGranularity.Card ? run.Outcome : OutcomeQualitySignal.Unknown);

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static int? Size(int? lines) => lines is null ? null : lines <= 50 ? 0 : lines <= 200 ? 1 : lines <= 500 ? 2 : 3;
}
