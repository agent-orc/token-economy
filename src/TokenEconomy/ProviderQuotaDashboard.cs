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

/// <summary>Token share for a model tier within one provider's active quota window.</summary>
public sealed record ModelTierShare(string Model, long Tokens, decimal Percent);

/// <summary>Dashboard row for one provider, including rate, tier shares, and projected quota exhaustion.</summary>
public sealed record ProviderQuotaDashboardRow(
    string Provider, long TrailingTokens, decimal TokensPerHour, long QuotaWindowTokens,
    long QuotaMarkTokens, decimal QuotaMarkPercent, long TokensUntilMark,
    DateTime? ProjectedMarkAtUtc, QuotaVisualState State, IReadOnlyList<ModelTierShare> ModelShares);

/// <summary>Structured event emitted after a quota dashboard is built.</summary>
public sealed record ProviderQuotaDashboardEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>
/// Builds provider quota dashboard rows from imported task-storage records. Token totals include input,
/// output, and cache token fields because all are observable consumption supplied by the importer.
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
            var shares = quotaRecords.GroupBy(r => r.Model, StringComparer.OrdinalIgnoreCase).Select(g =>
            {
                var tokens = g.Sum(TokenTotal);
                return new ModelTierShare(g.Key, tokens, quotaTokens == 0 ? 0 : tokens * 100m / quotaTokens);
            }).OrderByDescending(s => s.Tokens).ThenBy(s => s.Model, StringComparer.OrdinalIgnoreCase).ToList();
            return new ProviderQuotaDashboardRow(mark.Provider, trailing, rate, quotaTokens, mark.Tokens, percent, remaining, projected, state, shares);
        }).ToList();
        timer.Stop();
        EventOccurred?.Invoke(new("provider_quota.dashboard.built", new Dictionary<string, object?>
        { ["providerCount"] = rows.Count, ["recordCount"] = usable.Count, ["elapsedMs"] = timer.ElapsedMilliseconds }));
        return rows;
    }

    private static long TokenTotal(AgentStudioRunRecord record) => checked(record.Usage.Input + record.Usage.Output + record.Usage.CacheRead + record.Usage.CacheWrite);
}
