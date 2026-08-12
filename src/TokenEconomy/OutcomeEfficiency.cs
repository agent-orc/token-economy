namespace TokenEconomy;

#pragma warning disable CS1591

public enum DeliveryOutcome { Unknown, Accepted, Rejected }
public enum AttemptFailureClass { None, Semantic, Substrate, Unknown }

/// <summary>One actual, dated attempt contributing to delivery efficiency.</summary>
public sealed record DeliveryAttemptEfficiencyInput
{
    public required string DeliveryId { get; init; }
    public required string AttemptId { get; init; }
    public required int AttemptOrdinal { get; init; }
    public required ModelId Model { get; init; }
    public required DateTime ExecutedAtUtc { get; init; }
    public TokenUsage? Usage { get; init; }
    public required DeliveryOutcome Outcome { get; init; }
    public required AttemptFailureClass FailureClass { get; init; }
    public string WorkflowRole { get; init; } = "core-task";
}

public sealed record EfficiencyScope
{
    public required string TaskClass { get; init; }
    public required string AcceptanceDefinition { get; init; }
    public IReadOnlyList<string> IncludedWorkflowRoles { get; init; } = ["core-task"];
}

public sealed record MetricCoverage(int Measured, int Total)
{
    public decimal Rate => Total == 0 ? 0 : (decimal)Measured / Total;
    public bool Complete => Measured == Total;
}

public sealed record TokenUsagePerOutcome(
    decimal Input,
    decimal Output,
    decimal CacheRead,
    decimal CacheWrite);

/// <summary>Observed tokens and dated list-price cost per externally accepted outcome.</summary>
public sealed record OutcomeEfficiencyReport
{
    public required EfficiencyScope Scope { get; init; }
    public required int DeliveriesStarted { get; init; }
    public required int DeliveriesAccepted { get; init; }
    public required int Attempts { get; init; }
    public required int RetryAttempts { get; init; }
    public decimal? TokensPerAcceptedOutcome { get; init; }
    public decimal? TokensPerStartedDelivery { get; init; }
    public TokenUsagePerOutcome? TokenComponentsPerAcceptedOutcome { get; init; }
    public decimal? CostPerAcceptedDelivery { get; init; }
    public decimal? InitialCostPerStartedDelivery { get; init; }
    public decimal? RetryAdjustedCostPerStartedDelivery { get; init; }
    public decimal? RetryCostUplift { get; init; }
    public decimal AttemptsPerStartedDelivery { get; init; }
    public decimal? AttemptsPerAcceptedDelivery { get; init; }
    public decimal RetriesPerStartedDelivery { get; init; }
    public decimal SemanticRetryRate { get; init; }
    public required MetricCoverage TokenCoverage { get; init; }
    public required MetricCoverage CostCoverage { get; init; }
    public decimal KnownPartialCost { get; init; }
    public string? Currency { get; init; }
    public bool Unconfirmed { get; init; }
    public string? Caveat { get; init; }
    public required IReadOnlyList<string> UnknownReasons { get; init; }
}

/// <summary>Pure retry-aware efficiency calculations; no probes, launches, or writes.</summary>
public static class OutcomeEfficiency
{
    public static OutcomeEfficiencyReport ComputeObserved(
        IEnumerable<DeliveryAttemptEfficiencyInput> attempts,
        ModelPriceCatalog catalog,
        EfficiencyScope scope)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(scope.TaskClass) || string.IsNullOrWhiteSpace(scope.AcceptanceDefinition))
            throw new ArgumentException("Efficiency scope requires task class and acceptance definition.", nameof(scope));

        var rows = Deduplicate(attempts).OrderBy(item => item.DeliveryId, StringComparer.Ordinal)
            .ThenBy(item => item.AttemptOrdinal).ToArray();
        Validate(rows);
        if (rows.Any(item => !scope.IncludedWorkflowRoles.Contains(item.WorkflowRole, StringComparer.Ordinal)))
            throw new ArgumentException("An attempt workflow role is outside the declared efficiency scope.", nameof(attempts));
        var deliveries = rows.GroupBy(item => item.DeliveryId, StringComparer.Ordinal).ToArray();
        var accepted = deliveries.Count(group => group.Any(item => item.Outcome == DeliveryOutcome.Accepted));
        var retries = rows.Count(item => item.AttemptOrdinal > 1);
        var tokenRows = rows.Where(item => item.Usage is not null).ToArray();
        var componentTotals = new TokenUsage(
            tokenRows.Sum(item => Positive(item.Usage!.Value.Input)),
            tokenRows.Sum(item => Positive(item.Usage!.Value.Output)),
            tokenRows.Sum(item => Positive(item.Usage!.Value.CacheRead)),
            tokenRows.Sum(item => Positive(item.Usage!.Value.CacheWrite)));
        var totalTokens = componentTotals.Input + componentTotals.Output + componentTotals.CacheRead + componentTotals.CacheWrite;

        var unknown = new List<string>();
        if (tokenRows.Length != rows.Length) unknown.Add("Token usage coverage is incomplete.");
        var costs = new List<(DeliveryAttemptEfficiencyInput Attempt, CostBreakdown Cost)>();
        foreach (var row in rows)
        {
            if (row.Usage is null) continue;
            costs.Add((row, catalog.ComputeCost(row.Model, row.Usage.Value, row.ExecutedAtUtc)));
        }
        var resolvedCosts = costs.Where(item => item.Cost.Status == PriceStatus.Resolved).ToArray();
        var currencies = resolvedCosts.Select(item => item.Cost.Currency).Distinct(StringComparer.Ordinal).ToArray();
        var costComplete = resolvedCosts.Length == rows.Length && currencies.Length <= 1;
        if (resolvedCosts.Length != rows.Length) unknown.Add("Dated list-price coverage is incomplete.");
        if (currencies.Length > 1) unknown.Add("Resolved attempts use mixed currencies.");
        if (accepted == 0) unknown.Add("No delivery reached the declared external acceptance outcome.");
        var totalCost = resolvedCosts.Sum(item => item.Cost.Total!.Value);
        var initialCost = resolvedCosts.Where(item => item.Attempt.AttemptOrdinal == 1).Sum(item => item.Cost.Total!.Value);
        var deliveriesStarted = deliveries.Length;
        var retryAdjusted = costComplete && deliveriesStarted > 0 ? totalCost / deliveriesStarted : (decimal?)null;
        var initialPerDelivery = costComplete && deliveriesStarted > 0 ? initialCost / deliveriesStarted : (decimal?)null;

        return new()
        {
            Scope = scope,
            DeliveriesStarted = deliveriesStarted,
            DeliveriesAccepted = accepted,
            Attempts = rows.Length,
            RetryAttempts = retries,
            TokensPerAcceptedOutcome = tokenRows.Length == rows.Length && accepted > 0 ? (decimal)totalTokens / accepted : null,
            TokensPerStartedDelivery = tokenRows.Length == rows.Length && deliveriesStarted > 0 ? (decimal)totalTokens / deliveriesStarted : null,
            TokenComponentsPerAcceptedOutcome = tokenRows.Length == rows.Length && accepted > 0
                ? new TokenUsagePerOutcome((decimal)componentTotals.Input / accepted, (decimal)componentTotals.Output / accepted,
                    (decimal)componentTotals.CacheRead / accepted, (decimal)componentTotals.CacheWrite / accepted)
                : null,
            CostPerAcceptedDelivery = costComplete && accepted > 0 ? totalCost / accepted : null,
            InitialCostPerStartedDelivery = initialPerDelivery,
            RetryAdjustedCostPerStartedDelivery = retryAdjusted,
            RetryCostUplift = retryAdjusted is not null && initialPerDelivery is > 0
                ? retryAdjusted / initialPerDelivery - 1 : null,
            AttemptsPerStartedDelivery = deliveriesStarted == 0 ? 0 : (decimal)rows.Length / deliveriesStarted,
            AttemptsPerAcceptedDelivery = accepted == 0 ? null : (decimal)rows.Length / accepted,
            RetriesPerStartedDelivery = deliveriesStarted == 0 ? 0 : (decimal)retries / deliveriesStarted,
            SemanticRetryRate = deliveriesStarted == 0 ? 0 : (decimal)deliveries.Count(group =>
                group.Count() > 1 && group.Any(item => item.FailureClass == AttemptFailureClass.Semantic)) / deliveriesStarted,
            TokenCoverage = new(tokenRows.Length, rows.Length),
            CostCoverage = new(resolvedCosts.Length, rows.Length),
            KnownPartialCost = totalCost,
            Currency = currencies.Length == 1 ? currencies[0] : null,
            Unconfirmed = resolvedCosts.Any(item => item.Cost.Unconfirmed),
            Caveat = costComplete ? ModelPrice.EstimatedListPricesCaveat : null,
            UnknownReasons = unknown,
        };
    }

    private static DeliveryAttemptEfficiencyInput[] Deduplicate(IEnumerable<DeliveryAttemptEfficiencyInput> source)
    {
        var result = new List<DeliveryAttemptEfficiencyInput>();
        foreach (var group in source.GroupBy(item => item.AttemptId, StringComparer.Ordinal))
        {
            var values = group.Distinct().ToArray();
            if (values.Length > 1)
                throw new ArgumentException($"Attempt id '{group.Key}' has conflicting replay content.", nameof(source));
            result.Add(values[0]);
        }
        return result.ToArray();
    }

    private static void Validate(IReadOnlyList<DeliveryAttemptEfficiencyInput> rows)
    {
        if (rows.Any(item => string.IsNullOrWhiteSpace(item.DeliveryId) || string.IsNullOrWhiteSpace(item.AttemptId)))
            throw new ArgumentException("Delivery and attempt ids are required.", nameof(rows));
        if (rows.Any(item => item.ExecutedAtUtc.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Every execution timestamp must be UTC.", nameof(rows));
        foreach (var delivery in rows.GroupBy(item => item.DeliveryId, StringComparer.Ordinal))
        {
            var ordinals = delivery.Select(item => item.AttemptOrdinal).Order().ToArray();
            if (!ordinals.SequenceEqual(Enumerable.Range(1, ordinals.Length)))
                throw new ArgumentException($"Delivery '{delivery.Key}' has repeated or non-contiguous attempt ordinals.", nameof(rows));
            if (delivery.Count(item => item.Outcome == DeliveryOutcome.Accepted) > 1)
                throw new ArgumentException($"Delivery '{delivery.Key}' credits more than one acceptance.", nameof(rows));
            var accepted = delivery.SingleOrDefault(item => item.Outcome == DeliveryOutcome.Accepted);
            if (accepted is not null && accepted.AttemptOrdinal != ordinals[^1])
                throw new ArgumentException($"Delivery '{delivery.Key}' credits acceptance before its terminal attempt.", nameof(rows));
        }
    }

    private static long Positive(long value) => Math.Max(0, value);
}

#pragma warning restore CS1591
