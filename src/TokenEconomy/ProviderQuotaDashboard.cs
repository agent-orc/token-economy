using System.Diagnostics;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Visual state for a provider's configured quota mark.</summary>
public enum QuotaVisualState { Ok, Warning, Critical }

/// <summary>Configurable percentage boundaries for quota dashboard markers.</summary>
public sealed record QuotaThresholds(decimal WarningPercent = 75m, decimal CriticalPercent = 90m)
{
    public void Validate()
    {
        if (WarningPercent is < 0 or > 100 || CriticalPercent is < 0 or > 100 || WarningPercent > CriticalPercent)
            throw new ArgumentOutOfRangeException(nameof(WarningPercent), "Thresholds must be between 0 and 100, with warning no greater than critical.");
    }
}

/// <summary>Provider-specific quota mark, expressed as tokens consumed in the active quota window.</summary>
public sealed record ProviderQuotaMark(string Provider, long Tokens)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("Provider is required.", nameof(Provider));
        if (Tokens <= 0) throw new ArgumentOutOfRangeException(nameof(Tokens), "Quota mark must be positive.");
    }
}

/// <summary>Input settings for a historical provider quota dashboard.</summary>
public sealed record ProviderQuotaDashboardOptions(
    DateTime AsOfUtc,
    TimeSpan TrailingWindow,
    TimeSpan QuotaWindow,
    IReadOnlyCollection<ProviderQuotaMark> QuotaMarks,
    QuotaThresholds? Thresholds = null)
{
    public void Validate()
    {
        if (AsOfUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("AsOfUtc must be UTC.", nameof(AsOfUtc));
        if (TrailingWindow <= TimeSpan.Zero || QuotaWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(TrailingWindow), "Windows must be positive.");
        (Thresholds ?? new()).Validate();
        foreach (var mark in QuotaMarks) mark.Validate();
        if (QuotaMarks.GroupBy(m => m.Provider, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("Only one quota mark is allowed per provider.", nameof(QuotaMarks));
    }
}

/// <summary>Token share for a capability tier within one provider's active quota window.</summary>
public sealed record ModelTierShare(string Tier, long Tokens, decimal Percent);

/// <summary>Dashboard row for one provider, including rate, tier shares, and projected quota exhaustion.</summary>
public sealed record ProviderQuotaDashboardRow(
    string Provider, long TrailingTokens, decimal TokensPerHour, long QuotaWindowTokens,
    long QuotaMarkTokens, decimal QuotaMarkPercent, long TokensUntilMark,
    DateTime? ProjectedMarkAtUtc, QuotaVisualState State, IReadOnlyList<ModelTierShare> ModelShares);

/// <summary>Structured event emitted after a quota dashboard is built.</summary>
public sealed record ProviderQuotaDashboardEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>Observed launch availability for a provider/CLI pair.</summary>
public enum ProviderCliAvailability { Available, Unavailable, Unknown }

/// <summary>Whether an observation is usable at the snapshot's decision time.</summary>
public enum SnapshotFreshness { Fresh, Stale, Missing, Suspicious }

/// <summary>Conservative warning state for a provider or one of its quota windows.</summary>
public enum AvailabilityWarningState { Healthy, Warning, Critical, Unknown }

/// <summary>Cost coverage for the explicitly named models at the decision time.</summary>
public enum SnapshotCostStatus { Priced, Unconfirmed, Unpriced, Unknown }

/// <summary>Identifies whether a snapshot value came from telemetry or was calculated from it.</summary>
public enum SnapshotValueOrigin { Observed, Inferred }

/// <summary>An observed provider/CLI status and the models whose current price status must be reported.</summary>
public sealed record ProviderCliObservation(
    string Provider,
    string CliType,
    ProviderCliAvailability Availability,
    DateTime? ObservedAtUtc,
    IReadOnlyCollection<string> ModelIds,
    string? Detail = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("Provider is required.", nameof(Provider));
        if (string.IsNullOrWhiteSpace(CliType)) throw new ArgumentException("CLI type is required.", nameof(CliType));
        if (ModelIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Model ids cannot be blank.", nameof(ModelIds));
    }
}

/// <summary>
/// One provider-reported quota measurement. The window id is provider-defined and remains distinct
/// from every other window; null fields represent missing telemetry rather than zero.
/// </summary>
public sealed record ProviderQuotaWindowObservation(
    string Provider,
    string CliType,
    string WindowId,
    long? UsedTokens,
    long? LimitTokens,
    DateTime? ObservedAtUtc,
    DateTime? ResetsAtUtc,
    DateTime? WindowStartedAtUtc = null)
{
    public void ValidateIdentity()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("Provider is required.", nameof(Provider));
        if (string.IsNullOrWhiteSpace(CliType)) throw new ArgumentException("CLI type is required.", nameof(CliType));
        if (string.IsNullOrWhiteSpace(WindowId)) throw new ArgumentException("Window id is required.", nameof(WindowId));
    }
}

/// <summary>Inputs for a routing-grade snapshot taken at one explicit decision instant.</summary>
public sealed record ProviderAvailabilitySnapshotOptions(
    DateTime DecisionAtUtc,
    TimeSpan TrailingWindow,
    TimeSpan MaximumObservationAge,
    IReadOnlyCollection<ProviderCliObservation> Providers,
    IReadOnlyCollection<ProviderQuotaWindowObservation> QuotaWindows,
    QuotaThresholds? Thresholds = null)
{
    public void Validate()
    {
        if (DecisionAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("DecisionAtUtc must be UTC.", nameof(DecisionAtUtc));
        if (TrailingWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(TrailingWindow), "Trailing window must be positive.");
        if (MaximumObservationAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumObservationAge), "Maximum observation age must be positive.");
        (Thresholds ?? new()).Validate();
        foreach (var provider in Providers) provider.Validate();
        foreach (var window in QuotaWindows) window.ValidateIdentity();
        if (Providers.GroupBy(p => (p.Provider.ToLowerInvariant(), p.CliType.ToLowerInvariant())).Any(g => g.Count() > 1))
            throw new ArgumentException("Only one availability observation is allowed per provider/CLI pair.", nameof(Providers));
        if (QuotaWindows.GroupBy(w => (w.Provider.ToLowerInvariant(), w.CliType.ToLowerInvariant(), w.WindowId.ToLowerInvariant())).Any(g => g.Count() > 1))
            throw new ArgumentException("Quota window ids must be unique within a provider/CLI pair.", nameof(QuotaWindows));
        var providerKeys = Providers.Select(p => (p.Provider.ToLowerInvariant(), p.CliType.ToLowerInvariant())).ToHashSet();
        if (QuotaWindows.Any(w => !providerKeys.Contains((w.Provider.ToLowerInvariant(), w.CliType.ToLowerInvariant()))))
            throw new ArgumentException("Every quota window must belong to a configured provider/CLI pair.", nameof(QuotaWindows));
    }
}

/// <summary>One model's catalog resolution at the snapshot decision time.</summary>
public sealed record ModelCostAtDecision(string ModelId, PriceStatus PriceStatus, bool Unconfirmed);

/// <summary>Conservative aggregate cost status plus the model resolutions that produced it.</summary>
public sealed record ProviderCostAtDecision(
    DateTime DecisionAtUtc,
    SnapshotCostStatus Status,
    IReadOnlyList<ModelCostAtDecision> Models);

/// <summary>Provider-reported quota usage. It is never replaced by an estimate from imported runs.</summary>
public sealed record ObservedQuotaUsage(
    SnapshotValueOrigin Origin,
    long UsedTokens,
    long LimitTokens,
    long HeadroomTokens,
    decimal UsedPercent,
    DateTime ObservedAtUtc);

/// <summary>A rate-based projection derived from imported run telemetry, kept separate from observed quota.</summary>
public sealed record InferredQuotaProjection(
    SnapshotValueOrigin Origin,
    TimeSpan BasedOnTrailingWindow,
    decimal TokensPerHour,
    DateTime ProjectedExhaustionAtUtc,
    bool ExhaustsBeforeReset);

/// <summary>Routing-grade state for one named quota window.</summary>
public sealed record ProviderQuotaWindowSnapshot(
    string WindowId,
    DateTime? WindowStartedAtUtc,
    DateTime? ResetsAtUtc,
    SnapshotFreshness Freshness,
    AvailabilityWarningState WarningState,
    ObservedQuotaUsage? Usage,
    InferredQuotaProjection? Projection);

/// <summary>Routing-grade state for one provider/CLI pair.</summary>
public sealed record ProviderAvailabilitySnapshotRow(
    string Provider,
    string CliType,
    ProviderCliAvailability Availability,
    string? AvailabilityDetail,
    DateTime? AvailabilityObservedAtUtc,
    SnapshotFreshness Freshness,
    AvailabilityWarningState WarningState,
    long TrailingTokens,
    decimal TokensPerHour,
    ProviderCostAtDecision Cost,
    IReadOnlyList<ProviderQuotaWindowSnapshot> QuotaWindows,
    IReadOnlyList<ModelTierShare> TrailingModelShares);

/// <summary>
/// Immutable availability evidence at a decision instant. It describes routing inputs only and does
/// not select, rank, or downgrade a model.
/// </summary>
public sealed record ProviderAvailabilitySnapshot(
    DateTime DecisionAtUtc,
    TimeSpan TrailingWindow,
    TimeSpan MaximumObservationAge,
    IReadOnlyList<ProviderAvailabilitySnapshotRow> Providers);

/// <summary>
/// Builds routing-grade availability snapshots and the retained historical quota-dashboard rows.
/// Imported token totals include input, output, and cache fields. In a routing-grade snapshot those
/// runs inform only the explicitly inferred rate; provider-observed usage remains authoritative.
/// </summary>
public sealed class ProviderQuotaDashboardBuilder
{
    private readonly ModelPriceCatalog _prices;

    public ProviderQuotaDashboardBuilder(ModelPriceCatalog? prices = null) => _prices = prices ?? ModelPriceCatalog.Default;

    public event Action<ProviderQuotaDashboardEvent>? EventOccurred;

    /// <summary>
    /// Builds a conservative provider/CLI availability snapshot. Quota usage comes only from the
    /// supplied observations; imported runs contribute the separately labelled trailing-rate projection.
    /// </summary>
    public ProviderAvailabilitySnapshot BuildSnapshot(
        IEnumerable<AgentStudioRunRecord> records,
        ProviderAvailabilitySnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var timer = Stopwatch.StartNew();
        var imported = records.Where(r => r.ObservedAtUtc <= options.DecisionAtUtc).ToArray();
        var thresholds = options.Thresholds ?? new();
        var trailingStart = options.DecisionAtUtc - options.TrailingWindow;

        var rows = options.Providers
            .OrderBy(p => p.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.CliType, StringComparer.OrdinalIgnoreCase)
            .Select(provider =>
            {
                var providerRuns = imported.Where(r =>
                    string.Equals(r.Provider, provider.Provider, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.CliType, provider.CliType, StringComparison.OrdinalIgnoreCase)).ToArray();
                var trailingRuns = providerRuns
                    .Where(r => r.TokenUsageAvailable && r.ExecutedAtUtc >= trailingStart && r.ExecutedAtUtc <= options.DecisionAtUtc)
                    .ToArray();
                var trailingTokens = trailingRuns.Sum(TokenTotal);
                var rate = trailingTokens / (decimal)options.TrailingWindow.TotalHours;
                var windowSnapshots = options.QuotaWindows
                    .Where(w => string.Equals(w.Provider, provider.Provider, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(w.CliType, provider.CliType, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.WindowId, StringComparer.OrdinalIgnoreCase)
                    .Select(window => BuildWindow(window, rate, thresholds, options))
                    .ToArray();
                var availabilityFreshness = FreshnessOf(provider.ObservedAtUtc, options);
                var freshness = WorstFreshness([availabilityFreshness, .. windowSnapshots.Select(w => w.Freshness)]);
                var cost = ResolveCost(provider.ModelIds, options.DecisionAtUtc);
                var warning = OverallWarning(provider.Availability, availabilityFreshness, windowSnapshots, cost.Status);
                var shares = trailingRuns.GroupBy(r => ResolveTier(r.Model), StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var tokens = group.Sum(TokenTotal);
                        return new ModelTierShare(group.Key, tokens, trailingTokens == 0 ? 0 : tokens * 100m / trailingTokens);
                    })
                    .OrderByDescending(share => share.Tokens)
                    .ThenBy(share => share.Tier, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new ProviderAvailabilitySnapshotRow(
                    provider.Provider, provider.CliType, provider.Availability, provider.Detail,
                    provider.ObservedAtUtc, freshness, warning, trailingTokens, rate, cost,
                    windowSnapshots, shares);
            })
            .ToArray();

        timer.Stop();
        EventOccurred?.Invoke(new("provider_availability.snapshot.built", new Dictionary<string, object?>
        {
            ["providerCliCount"] = rows.Length,
            ["quotaWindowCount"] = rows.Sum(row => row.QuotaWindows.Count),
            ["recordCount"] = imported.Length,
            ["decisionAtUtc"] = options.DecisionAtUtc,
            ["elapsedMs"] = timer.ElapsedMilliseconds,
        }));
        return new(options.DecisionAtUtc, options.TrailingWindow, options.MaximumObservationAge, rows);
    }

    public IReadOnlyList<ProviderQuotaDashboardRow> Build(IEnumerable<AgentStudioRunRecord> records, ProviderQuotaDashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var timer = Stopwatch.StartNew();
        var marks = options.QuotaMarks.ToDictionary(m => m.Provider, StringComparer.OrdinalIgnoreCase);
        var usable = records.Where(r => !string.IsNullOrWhiteSpace(r.Provider) && r.ObservedAtUtc <= options.AsOfUtc).ToList();
        var trailingStart = options.AsOfUtc - options.TrailingWindow;
        var quotaStart = options.AsOfUtc - options.QuotaWindow;
        var thresholds = options.Thresholds ?? new();

        var rows = marks.Values.OrderBy(m => m.Provider, StringComparer.OrdinalIgnoreCase).Select(mark =>
        {
            var providerRecords = usable.Where(r => string.Equals(r.Provider, mark.Provider, StringComparison.OrdinalIgnoreCase)).ToList();
            var trailing = providerRecords.Where(r => r.ObservedAtUtc >= trailingStart).Sum(TokenTotal);
            var quotaRecords = providerRecords.Where(r => r.ObservedAtUtc >= quotaStart).ToList();
            var quotaTokens = quotaRecords.Sum(TokenTotal);
            var percent = Math.Min(100m, quotaTokens * 100m / mark.Tokens);
            var state = percent >= thresholds.CriticalPercent ? QuotaVisualState.Critical : percent >= thresholds.WarningPercent ? QuotaVisualState.Warning : QuotaVisualState.Ok;
            var rate = trailing * 1m / (decimal)options.TrailingWindow.TotalHours;
            var remaining = Math.Max(0, mark.Tokens - quotaTokens);
            DateTime? projected = rate > 0 && remaining > 0 ? options.AsOfUtc.AddHours((double)(remaining / rate)) : null;
            var shares = quotaRecords.GroupBy(r => ResolveTier(r.Model), StringComparer.OrdinalIgnoreCase).Select(g =>
            {
                var tokens = g.Sum(TokenTotal);
                return new ModelTierShare(g.Key, tokens, quotaTokens == 0 ? 0 : tokens * 100m / quotaTokens);
            }).OrderByDescending(s => s.Tokens).ThenBy(s => s.Tier, StringComparer.OrdinalIgnoreCase).ToList();
            return new ProviderQuotaDashboardRow(mark.Provider, trailing, rate, quotaTokens, mark.Tokens, percent, remaining, projected, state, shares);
        }).ToList();
        timer.Stop();
        EventOccurred?.Invoke(new("provider_quota.dashboard.built", new Dictionary<string, object?>
        { ["providerCount"] = rows.Count, ["recordCount"] = usable.Count, ["elapsedMs"] = timer.ElapsedMilliseconds }));
        return rows;
    }

    private static long TokenTotal(AgentStudioRunRecord record) => checked(record.Usage.Input + record.Usage.Output + record.Usage.CacheRead + record.Usage.CacheWrite);

    private ProviderCostAtDecision ResolveCost(IReadOnlyCollection<string> modelIds, DateTime decisionAtUtc)
    {
        var models = modelIds.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .Select(model =>
            {
                var resolution = _prices.ResolvePrice(model, decisionAtUtc);
                return new ModelCostAtDecision(model, resolution.Status, resolution.Price?.Unconfirmed ?? false);
            })
            .ToArray();
        var status = models.Length == 0 || models.Any(model => model.PriceStatus == PriceStatus.UnknownModel)
            ? SnapshotCostStatus.Unknown
            : models.Any(model => model.PriceStatus == PriceStatus.NoPriceForDate)
                ? SnapshotCostStatus.Unpriced
                : models.Any(model => model.Unconfirmed)
                    ? SnapshotCostStatus.Unconfirmed
                    : SnapshotCostStatus.Priced;
        return new(decisionAtUtc, status, models);
    }

    private static ProviderQuotaWindowSnapshot BuildWindow(
        ProviderQuotaWindowObservation window,
        decimal rate,
        QuotaThresholds thresholds,
        ProviderAvailabilitySnapshotOptions options)
    {
        var freshness = FreshnessOf(window.ObservedAtUtc, options);
        if (window.UsedTokens is < 0 || window.LimitTokens is <= 0 ||
            window.ResetsAtUtc is { Kind: not DateTimeKind.Utc } ||
            window.WindowStartedAtUtc is { Kind: not DateTimeKind.Utc } ||
            window.WindowStartedAtUtc is { } started && started > options.DecisionAtUtc ||
            window.WindowStartedAtUtc is { } windowStart && window.ResetsAtUtc is { } windowReset && windowStart >= windowReset ||
            window.ResetsAtUtc is { } reset && window.ObservedAtUtc is { } observed && reset < observed)
            freshness = SnapshotFreshness.Suspicious;
        else if (window.UsedTokens is null || window.LimitTokens is null || window.ObservedAtUtc is null || window.ResetsAtUtc is null)
            freshness = WorstFreshness([freshness, SnapshotFreshness.Missing]);
        else if (window.ResetsAtUtc is { } expired && expired <= options.DecisionAtUtc && window.ObservedAtUtc < expired)
            freshness = WorstFreshness([freshness, SnapshotFreshness.Stale]);

        ObservedQuotaUsage? usage = null;
        if (window.UsedTokens is >= 0 and var used && window.LimitTokens is > 0 and var limit && window.ObservedAtUtc is { } observedAt)
        {
            usage = new(SnapshotValueOrigin.Observed, used, limit, Math.Max(0, limit - used), used * 100m / limit, observedAt);
        }

        var warning = freshness != SnapshotFreshness.Fresh || usage is null
            ? AvailabilityWarningState.Unknown
            : usage.UsedTokens >= usage.LimitTokens || usage.UsedPercent >= thresholds.CriticalPercent
                ? AvailabilityWarningState.Critical
                : usage.UsedPercent >= thresholds.WarningPercent
                    ? AvailabilityWarningState.Warning
                    : AvailabilityWarningState.Healthy;

        InferredQuotaProjection? projection = null;
        if (freshness == SnapshotFreshness.Fresh && usage is { HeadroomTokens: > 0 } && rate > 0)
        {
            var hours = usage.HeadroomTokens / rate;
            if (hours <= (decimal)(DateTime.MaxValue - options.DecisionAtUtc).TotalHours)
            {
                var projectedAt = options.DecisionAtUtc.AddHours((double)hours);
                projection = new(SnapshotValueOrigin.Inferred, options.TrailingWindow, rate, projectedAt,
                    window.ResetsAtUtc is null || projectedAt <= window.ResetsAtUtc);
            }
        }

        return new(window.WindowId, window.WindowStartedAtUtc, window.ResetsAtUtc, freshness, warning, usage, projection);
    }

    private static SnapshotFreshness FreshnessOf(DateTime? observedAtUtc, ProviderAvailabilitySnapshotOptions options)
    {
        if (observedAtUtc is null) return SnapshotFreshness.Missing;
        if (observedAtUtc.Value.Kind != DateTimeKind.Utc || observedAtUtc > options.DecisionAtUtc)
            return SnapshotFreshness.Suspicious;
        return options.DecisionAtUtc - observedAtUtc > options.MaximumObservationAge
            ? SnapshotFreshness.Stale
            : SnapshotFreshness.Fresh;
    }

    private static SnapshotFreshness WorstFreshness(IEnumerable<SnapshotFreshness> values)
        => values.OrderByDescending(FreshnessSeverity).FirstOrDefault();

    private static int FreshnessSeverity(SnapshotFreshness freshness) => freshness switch
    {
        SnapshotFreshness.Fresh => 0,
        SnapshotFreshness.Stale => 1,
        SnapshotFreshness.Missing => 2,
        SnapshotFreshness.Suspicious => 3,
        _ => 3,
    };

    private static AvailabilityWarningState OverallWarning(
        ProviderCliAvailability availability,
        SnapshotFreshness availabilityFreshness,
        IReadOnlyCollection<ProviderQuotaWindowSnapshot> windows,
        SnapshotCostStatus costStatus)
    {
        if (availability == ProviderCliAvailability.Unavailable || windows.Any(window => window.WarningState == AvailabilityWarningState.Critical))
            return AvailabilityWarningState.Critical;
        if (availability != ProviderCliAvailability.Available || availabilityFreshness != SnapshotFreshness.Fresh ||
            windows.Count == 0 || windows.Any(window => window.WarningState == AvailabilityWarningState.Unknown) ||
            costStatus != SnapshotCostStatus.Priced)
            return AvailabilityWarningState.Unknown;
        return windows.Any(window => window.WarningState == AvailabilityWarningState.Warning)
            ? AvailabilityWarningState.Warning
            : AvailabilityWarningState.Healthy;
    }

    // Imported ids can be aliases. The matrix resolves those aliases and keeps the dashboard's
    // capability vocabulary aligned with model routing. Unprofiled imports remain visible.
    private static string ResolveTier(string? model) => ModelEfficiencyMatrix.Default.Find(model)?.Tier.ToString() ?? "Unknown";
}
