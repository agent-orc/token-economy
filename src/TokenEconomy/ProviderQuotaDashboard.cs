using System.Diagnostics;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Overall visual severity for a provider/CLI quota-window snapshot.</summary>
public enum QuotaVisualState { Ok, Warning, Critical }

/// <summary>Whether a provider CLI was usable when the routing decision was made.</summary>
public enum ProviderCliAvailability { Available, Unavailable, Unknown }

/// <summary>Quality/age classification for an availability or quota observation.</summary>
public enum AvailabilityFreshness { Fresh, Stale, Missing, Suspicious }

/// <summary>Origin of the current quota usage value. A projection is never reported as an observation.</summary>
public enum QuotaUsageSource { ObservedProviderMeter, InferredFromImportedRuns, Missing }

/// <summary>Decision-time price coverage for the models represented by a provider/CLI row.</summary>
public enum ProviderCostStatus { Priced, PartiallyPriced, Unconfirmed, Unpriced, Unknown }

/// <summary>Composable reasons why a snapshot must not be treated as healthy.</summary>
[Flags]
public enum ProviderQuotaWarning
{
    None = 0,
    NearCap = 1 << 0,
    Exhausted = 1 << 1,
    Stale = 1 << 2,
    Missing = 1 << 3,
    Suspicious = 1 << 4,
    Unavailable = 1 << 5,
    UnknownAvailability = 1 << 6,
    UnknownCost = 1 << 7,
    UnpricedCost = 1 << 8,
    InferredQuota = 1 << 9,
}

/// <summary>Configurable percentage boundaries for quota dashboard markers.</summary>
public sealed record QuotaThresholds(decimal WarningPercent = 75m, decimal CriticalPercent = 90m)
{
    public void Validate()
    {
        if (WarningPercent is < 0 or > 100 || CriticalPercent is < 0 or > 100 || WarningPercent > CriticalPercent)
            throw new ArgumentOutOfRangeException(nameof(WarningPercent), "Thresholds must be between 0 and 100, with warning no greater than critical.");
    }
}

/// <summary>
/// One quota window for a provider/CLI. <see cref="Tokens"/> is a configured mark, not a claim
/// that the provider publishes an absolute token limit. Provider-meter usage can be supplied
/// explicitly; otherwise imported run tokens remain visibly inferred.
/// </summary>
public sealed record ProviderQuotaMark(string Provider, long Tokens)
{
    public string? Cli { get; init; }
    public string WindowId { get; init; } = "default";
    public string WindowLabel { get; init; } = "Quota window";
    public TimeSpan? WindowDuration { get; init; }
    public long? ObservedUsedTokens { get; init; }
    public DateTime? ObservedAtUtc { get; init; }
    public DateTime? ResetsAtUtc { get; init; }
    public bool Suspicious { get; init; }
    public IReadOnlyList<string> ModelIds { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("Provider is required.", nameof(Provider));
        if (string.IsNullOrWhiteSpace(WindowId)) throw new ArgumentException("Quota window id is required.", nameof(WindowId));
        if (string.IsNullOrWhiteSpace(WindowLabel)) throw new ArgumentException("Quota window label is required.", nameof(WindowLabel));
        if (Tokens <= 0) throw new ArgumentOutOfRangeException(nameof(Tokens), "Quota mark must be positive.");
        if (ObservedUsedTokens < 0) throw new ArgumentOutOfRangeException(nameof(ObservedUsedTokens), "Observed quota usage cannot be negative.");
        if (WindowDuration is { } duration && duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(WindowDuration), "Quota window duration must be positive.");
        ValidateUtc(ObservedAtUtc, nameof(ObservedAtUtc));
        ValidateUtc(ResetsAtUtc, nameof(ResetsAtUtc));
        if (ModelIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Model ids cannot be blank.", nameof(ModelIds));
    }

    private static void ValidateUtc(DateTime? value, string name)
    {
        if (value is { Kind: not DateTimeKind.Utc }) throw new ArgumentException($"{name} must be UTC.", name);
    }
}

/// <summary>Observed provider/CLI reachability at a particular instant.</summary>
public sealed record ProviderCliAvailabilityObservation(
    string Provider,
    string Cli,
    ProviderCliAvailability Availability,
    DateTime ObservedAtUtc,
    bool Suspicious = false,
    string? Detail = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("Provider is required.", nameof(Provider));
        if (string.IsNullOrWhiteSpace(Cli)) throw new ArgumentException("CLI is required.", nameof(Cli));
        if (ObservedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ObservedAtUtc must be UTC.", nameof(ObservedAtUtc));
    }
}

/// <summary>Input settings for a decision-time provider availability snapshot.</summary>
public sealed record ProviderQuotaDashboardOptions(
    DateTime AsOfUtc,
    TimeSpan TrailingWindow,
    TimeSpan QuotaWindow,
    IReadOnlyCollection<ProviderQuotaMark> QuotaMarks,
    QuotaThresholds? Thresholds = null)
{
    /// <summary>Explicit CLI probes. Missing probes produce unknown availability, never healthy state.</summary>
    public IReadOnlyCollection<ProviderCliAvailabilityObservation> AvailabilityObservations { get; init; } = [];

    /// <summary>Maximum age of a decision-grade provider or quota observation.</summary>
    public TimeSpan FreshnessLimit { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Dated catalog used to resolve represented models at <see cref="AsOfUtc"/>.</summary>
    public ModelPriceCatalog PriceCatalog { get; init; } = ModelPriceCatalog.Default;

    public void Validate()
    {
        if (AsOfUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("AsOfUtc must be UTC.", nameof(AsOfUtc));
        if (TrailingWindow <= TimeSpan.Zero || QuotaWindow <= TimeSpan.Zero || FreshnessLimit <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TrailingWindow), "Windows and freshness limit must be positive.");
        ArgumentNullException.ThrowIfNull(QuotaMarks);
        ArgumentNullException.ThrowIfNull(AvailabilityObservations);
        ArgumentNullException.ThrowIfNull(PriceCatalog);
        (Thresholds ?? new()).Validate();
        foreach (var mark in QuotaMarks) mark.Validate();
        foreach (var observation in AvailabilityObservations) observation.Validate();
        if (QuotaMarks.GroupBy(mark => (mark.Provider.ToUpperInvariant(), (mark.Cli ?? "").ToUpperInvariant(), mark.WindowId.ToUpperInvariant())).Any(group => group.Count() > 1))
            throw new ArgumentException("A provider/CLI quota-window identity may appear only once.", nameof(QuotaMarks));
        if (AvailabilityObservations.GroupBy(observation => (observation.Provider.ToUpperInvariant(), observation.Cli.ToUpperInvariant())).Any(group => group.Count() > 1))
            throw new ArgumentException("A provider/CLI availability observation may appear only once.", nameof(AvailabilityObservations));
    }
}

/// <summary>Token share for a capability tier within one provider's active quota window.</summary>
public sealed record ModelTierShare(string Tier, long Tokens, decimal Percent);

/// <summary>Current quota usage, identity, reset, and provenance. No projected value is stored here.</summary>
public sealed record ProviderQuotaObservation
{
    public required QuotaUsageSource Source { get; init; }
    public DateTime? ObservedAtUtc { get; init; }
    public required AvailabilityFreshness Freshness { get; init; }
    public long? UsedTokens { get; init; }
    public required long ConfiguredMarkTokens { get; init; }
    public decimal? UsedPercent { get; init; }
    public long? HeadroomTokens { get; init; }
    public DateTime? ResetsAtUtc { get; init; }
}

/// <summary>Rate-based inference derived at decision time, kept separate from the quota observation.</summary>
public sealed record ProviderQuotaProjection
{
    public required DateTime DerivedAtUtc { get; init; }
    public required TimeSpan RateWindow { get; init; }
    public required long TrailingTokens { get; init; }
    public required decimal TokensPerHour { get; init; }
    public DateTime? ProjectedExhaustionAtUtc { get; init; }
}

/// <summary>Price resolution for one represented model at the routing decision time.</summary>
public sealed record ProviderModelCostStatus(
    string ModelId,
    string? CanonicalModelId,
    PriceStatus Status,
    string? Currency,
    bool Unconfirmed);

/// <summary>Decision-time price coverage. It reports catalog status, not a zero-dollar usage estimate.</summary>
public sealed record ProviderCostSnapshot
{
    public required DateTime PricedAtUtc { get; init; }
    public required ProviderCostStatus Status { get; init; }
    public required IReadOnlyList<ProviderModelCostStatus> Models { get; init; }
}

/// <summary>
/// One provider/CLI/quota-window row. The legacy scalar fields are retained for consumers of the
/// historical dashboard; routing-grade consumers should use <see cref="QuotaObservation"/>,
/// <see cref="Projection"/>, and <see cref="CostSnapshot"/> so inference cannot masquerade as observation.
/// </summary>
public sealed record ProviderQuotaDashboardRow(
    string Provider, long TrailingTokens, decimal TokensPerHour, long QuotaWindowTokens,
    long QuotaMarkTokens, decimal QuotaMarkPercent, long TokensUntilMark,
    DateTime? ProjectedMarkAtUtc, QuotaVisualState State, IReadOnlyList<ModelTierShare> ModelShares)
{
    public string? Cli { get; init; }
    public string WindowId { get; init; } = "default";
    public string WindowLabel { get; init; } = "Quota window";
    public TimeSpan WindowDuration { get; init; }
    public DateTime DecisionAtUtc { get; init; }
    public ProviderCliAvailability Availability { get; init; } = ProviderCliAvailability.Unknown;
    public DateTime? AvailabilityObservedAtUtc { get; init; }
    public AvailabilityFreshness AvailabilityFreshness { get; init; } = AvailabilityFreshness.Missing;
    public ProviderQuotaWarning WarningState { get; init; } = ProviderQuotaWarning.Missing | ProviderQuotaWarning.UnknownAvailability | ProviderQuotaWarning.UnknownCost;
    public IReadOnlyList<string> WarningReasons { get; init; } = ["availability observation missing", "cost status unknown"];
    public ProviderQuotaObservation? QuotaObservation { get; init; }
    public ProviderQuotaProjection? Projection { get; init; }
    public ProviderCostSnapshot? CostSnapshot { get; init; }
}

/// <summary>Structured event emitted after a quota dashboard is built.</summary>
public sealed record ProviderQuotaDashboardEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>
/// Builds decision-time provider/CLI/quota-window snapshots from imported task-storage records and
/// explicit provider observations. It describes availability only and never selects a model.
/// </summary>
public sealed class ProviderQuotaDashboardBuilder
{
    public event Action<ProviderQuotaDashboardEvent>? EventOccurred;

    public IReadOnlyList<ProviderQuotaDashboardRow> Build(IEnumerable<AgentStudioRunRecord> records, ProviderQuotaDashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var timer = Stopwatch.StartNew();
        var usable = records.Where(record => !string.IsNullOrWhiteSpace(record.Provider) && record.ObservedAtUtc <= options.AsOfUtc).ToList();
        var thresholds = options.Thresholds ?? new();

        var rows = options.QuotaMarks
            .OrderBy(mark => mark.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mark => mark.Cli, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mark => mark.WindowId, StringComparer.OrdinalIgnoreCase)
            .Select(mark => BuildRow(usable, mark, options, thresholds))
            .ToList();
        timer.Stop();
        EventOccurred?.Invoke(new("provider_quota.availability_snapshot.built", new Dictionary<string, object?>
        {
            ["providerCount"] = rows.Select(row => row.Provider).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ["providerCliCount"] = rows.Select(row => (row.Provider.ToUpperInvariant(), (row.Cli ?? "").ToUpperInvariant())).Distinct().Count(),
            ["quotaWindowCount"] = rows.Count,
            ["recordCount"] = usable.Count,
            ["decisionAtUtc"] = options.AsOfUtc,
            ["elapsedMs"] = timer.ElapsedMilliseconds,
        }));
        return rows;
    }

    private static ProviderQuotaDashboardRow BuildRow(
        IReadOnlyList<AgentStudioRunRecord> records,
        ProviderQuotaMark mark,
        ProviderQuotaDashboardOptions options,
        QuotaThresholds thresholds)
    {
        var providerRecords = records.Where(record =>
            string.Equals(record.Provider, mark.Provider, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(mark.Cli) || string.Equals(record.CliType, mark.Cli, StringComparison.OrdinalIgnoreCase))).ToList();
        var recordsWithUsage = providerRecords.Where(record => record.TokenUsageAvailable).ToList();
        var duration = mark.WindowDuration ?? options.QuotaWindow;
        var trailingStart = options.AsOfUtc - options.TrailingWindow;
        var quotaStart = options.AsOfUtc - duration;
        var trailing = recordsWithUsage.Where(record => record.ObservedAtUtc >= trailingStart).Sum(TokenTotal);
        var quotaRecords = recordsWithUsage.Where(record => record.ObservedAtUtc >= quotaStart).ToList();
        var inferredQuotaTokens = quotaRecords.Sum(TokenTotal);
        var usedTokens = mark.ObservedUsedTokens ?? (quotaRecords.Count > 0 ? inferredQuotaTokens : null);
        var usageSource = mark.ObservedUsedTokens is not null
            ? QuotaUsageSource.ObservedProviderMeter
            : quotaRecords.Count > 0 ? QuotaUsageSource.InferredFromImportedRuns : QuotaUsageSource.Missing;
        var quotaObservedAt = mark.ObservedAtUtc ?? quotaRecords.OrderByDescending(record => record.ObservedAtUtc).FirstOrDefault()?.ObservedAtUtc;
        var quotaFreshness = Freshness(quotaObservedAt, mark.Suspicious, options);
        var percent = usedTokens is { } used ? used * 100m / mark.Tokens : 0m;
        var remaining = usedTokens is { } measured ? Math.Max(0, mark.Tokens - measured) : 0;
        var rate = trailing / (decimal)options.TrailingWindow.TotalHours;
        DateTime? projected = usedTokens is null ? null
            : usedTokens >= mark.Tokens ? options.AsOfUtc
            : rate > 0 ? options.AsOfUtc.AddHours((double)(remaining / rate))
            : null;

        var availabilityObservation = FindAvailability(mark, options.AvailabilityObservations);
        var availability = availabilityObservation?.Availability ?? ProviderCliAvailability.Unknown;
        var availabilityFreshness = availabilityObservation is null
            ? AvailabilityFreshness.Missing
            : Freshness(availabilityObservation.ObservedAtUtc, availabilityObservation.Suspicious, options);
        var models = mark.ModelIds.Count > 0
            ? mark.ModelIds.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : providerRecords.Where(record => !string.IsNullOrWhiteSpace(record.Model)).Select(record => record.Model!)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var cost = CostSnapshot(models, options);
        var warnings = Warnings(availability, availabilityFreshness, quotaFreshness, usageSource, percent, cost.Status, thresholds);
        var state = VisualState(warnings, percent, thresholds);
        var reasons = WarningReasons(warnings, availabilityObservation?.Detail);
        var shares = quotaRecords.GroupBy(record => ResolveTier(record.Model), StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var tokens = group.Sum(TokenTotal);
            return new ModelTierShare(group.Key, tokens, inferredQuotaTokens == 0 ? 0 : tokens * 100m / inferredQuotaTokens);
        }).OrderByDescending(share => share.Tokens).ThenBy(share => share.Tier, StringComparer.OrdinalIgnoreCase).ToList();

        var observation = new ProviderQuotaObservation
        {
            Source = usageSource,
            ObservedAtUtc = quotaObservedAt,
            Freshness = quotaFreshness,
            UsedTokens = usedTokens,
            ConfiguredMarkTokens = mark.Tokens,
            UsedPercent = usedTokens is null ? null : percent,
            HeadroomTokens = usedTokens is null ? null : remaining,
            ResetsAtUtc = mark.ResetsAtUtc,
        };
        var projection = new ProviderQuotaProjection
        {
            DerivedAtUtc = options.AsOfUtc,
            RateWindow = options.TrailingWindow,
            TrailingTokens = trailing,
            TokensPerHour = rate,
            ProjectedExhaustionAtUtc = projected,
        };
        return new ProviderQuotaDashboardRow(mark.Provider, trailing, rate, usedTokens ?? 0, mark.Tokens, percent, remaining, projected, state, shares)
        {
            Cli = mark.Cli,
            WindowId = mark.WindowId,
            WindowLabel = mark.WindowLabel,
            WindowDuration = duration,
            DecisionAtUtc = options.AsOfUtc,
            Availability = availability,
            AvailabilityObservedAtUtc = availabilityObservation?.ObservedAtUtc,
            AvailabilityFreshness = availabilityFreshness,
            WarningState = warnings,
            WarningReasons = reasons,
            QuotaObservation = observation,
            Projection = projection,
            CostSnapshot = cost,
        };
    }

    private static ProviderCliAvailabilityObservation? FindAvailability(
        ProviderQuotaMark mark,
        IReadOnlyCollection<ProviderCliAvailabilityObservation> observations)
    {
        var provider = observations.Where(observation => string.Equals(observation.Provider, mark.Provider, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(mark.Cli))
            return provider.SingleOrDefault(observation => string.Equals(observation.Cli, mark.Cli, StringComparison.OrdinalIgnoreCase));
        var candidates = provider.ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static AvailabilityFreshness Freshness(DateTime? observedAtUtc, bool suspicious, ProviderQuotaDashboardOptions options)
    {
        if (suspicious) return AvailabilityFreshness.Suspicious;
        if (observedAtUtc is null) return AvailabilityFreshness.Missing;
        var age = options.AsOfUtc - observedAtUtc.Value;
        if (age < TimeSpan.Zero) return AvailabilityFreshness.Suspicious;
        return age > options.FreshnessLimit ? AvailabilityFreshness.Stale : AvailabilityFreshness.Fresh;
    }

    private static ProviderCostSnapshot CostSnapshot(IReadOnlyList<string> models, ProviderQuotaDashboardOptions options)
    {
        var statuses = models.Select(model =>
        {
            var resolution = options.PriceCatalog.ResolvePrice(model, options.AsOfUtc);
            return new ProviderModelCostStatus(model, resolution.ModelId, resolution.Status, resolution.Price?.Currency, resolution.Price?.Unconfirmed ?? false);
        }).ToArray();
        var status = statuses.Length == 0 ? ProviderCostStatus.Unknown
            : statuses.All(model => model.Status == PriceStatus.Resolved && !model.Unconfirmed) ? ProviderCostStatus.Priced
            : statuses.All(model => model.Status == PriceStatus.Resolved) ? ProviderCostStatus.Unconfirmed
            : statuses.Any(model => model.Status == PriceStatus.Resolved) ? ProviderCostStatus.PartiallyPriced
            : statuses.Any(model => model.Status == PriceStatus.UnknownModel) ? ProviderCostStatus.Unknown
            : ProviderCostStatus.Unpriced;
        return new ProviderCostSnapshot { PricedAtUtc = options.AsOfUtc, Status = status, Models = statuses };
    }

    private static ProviderQuotaWarning Warnings(
        ProviderCliAvailability availability,
        AvailabilityFreshness availabilityFreshness,
        AvailabilityFreshness quotaFreshness,
        QuotaUsageSource usageSource,
        decimal percent,
        ProviderCostStatus costStatus,
        QuotaThresholds thresholds)
    {
        var warnings = ProviderQuotaWarning.None;
        if (availability == ProviderCliAvailability.Unavailable) warnings |= ProviderQuotaWarning.Unavailable;
        if (availability == ProviderCliAvailability.Unknown) warnings |= ProviderQuotaWarning.UnknownAvailability;
        if (availabilityFreshness == AvailabilityFreshness.Stale || quotaFreshness == AvailabilityFreshness.Stale) warnings |= ProviderQuotaWarning.Stale;
        if (availabilityFreshness == AvailabilityFreshness.Missing || quotaFreshness == AvailabilityFreshness.Missing) warnings |= ProviderQuotaWarning.Missing;
        if (availabilityFreshness == AvailabilityFreshness.Suspicious || quotaFreshness == AvailabilityFreshness.Suspicious) warnings |= ProviderQuotaWarning.Suspicious;
        if (usageSource == QuotaUsageSource.InferredFromImportedRuns) warnings |= ProviderQuotaWarning.InferredQuota;
        if (percent >= 100m) warnings |= ProviderQuotaWarning.Exhausted;
        else if (percent >= thresholds.WarningPercent) warnings |= ProviderQuotaWarning.NearCap;
        if (costStatus == ProviderCostStatus.Unknown) warnings |= ProviderQuotaWarning.UnknownCost;
        if (costStatus is ProviderCostStatus.Unpriced or ProviderCostStatus.PartiallyPriced or ProviderCostStatus.Unconfirmed) warnings |= ProviderQuotaWarning.UnpricedCost;
        return warnings;
    }

    private static QuotaVisualState VisualState(ProviderQuotaWarning warnings, decimal percent, QuotaThresholds thresholds)
    {
        var critical = ProviderQuotaWarning.Exhausted | ProviderQuotaWarning.Unavailable | ProviderQuotaWarning.Suspicious;
        if ((warnings & critical) != 0 || percent >= thresholds.CriticalPercent) return QuotaVisualState.Critical;
        return warnings == ProviderQuotaWarning.None ? QuotaVisualState.Ok : QuotaVisualState.Warning;
    }

    private static IReadOnlyList<string> WarningReasons(ProviderQuotaWarning warnings, string? availabilityDetail)
    {
        var reasons = new List<string>();
        Add(ProviderQuotaWarning.Exhausted, "quota mark exhausted");
        Add(ProviderQuotaWarning.NearCap, "quota mark near cap");
        Add(ProviderQuotaWarning.Unavailable, string.IsNullOrWhiteSpace(availabilityDetail) ? "provider CLI unavailable" : $"provider CLI unavailable: {availabilityDetail}");
        Add(ProviderQuotaWarning.UnknownAvailability, "provider CLI availability unknown");
        Add(ProviderQuotaWarning.Stale, "stale observation");
        Add(ProviderQuotaWarning.Missing, "required observation missing");
        Add(ProviderQuotaWarning.Suspicious, "suspicious observation");
        Add(ProviderQuotaWarning.InferredQuota, "quota usage inferred from imported runs");
        Add(ProviderQuotaWarning.UnknownCost, "cost status unknown");
        Add(ProviderQuotaWarning.UnpricedCost, "one or more models are unpriced or unconfirmed");
        return reasons;

        void Add(ProviderQuotaWarning flag, string reason)
        {
            if ((warnings & flag) != 0) reasons.Add(reason);
        }
    }

    private static long TokenTotal(AgentStudioRunRecord record) => checked(record.Usage.Input + record.Usage.Output + record.Usage.CacheRead + record.Usage.CacheWrite);

    // Imported ids can be aliases. The matrix resolves those aliases and keeps the dashboard's
    // capability vocabulary aligned with model routing. Unprofiled imports remain visible.
    private static string ResolveTier(string? model) => ModelEfficiencyMatrix.Default.Find(model)?.Tier.ToString() ?? "Unknown";
}
