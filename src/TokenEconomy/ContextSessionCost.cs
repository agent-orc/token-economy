namespace TokenEconomy;

/// <summary>Explicit workload assumptions. Gaps are request-start to request-start, including generation time.</summary>
public sealed record ContextSessionOptions
{
    /// <summary>Catalog model to price; this does not select a route.</summary>
    public required string ModelId { get; init; }
    /// <summary>UTC start; each subsequent call resolves its own dated price.</summary>
    public required DateTime StartUtc { get; init; }
    /// <summary>Reasoning label for provenance, not a fabricated token multiplier.</summary>
    public string ReasoningLevel { get; init; } = "medium";
    /// <summary>Context before the first call.</summary>
    public long InitialContext { get; init; }
    /// <summary>Retained tool results and file reads appended after each call.</summary>
    public long ToolTokensPerTurn { get; init; }
    /// <summary>Visible output, billed and retained in the next prompt.</summary>
    public long OutputTokensPerTurn { get; init; }
    /// <summary>Additional billed reasoning output; not assumed retained.</summary>
    public long ReasoningTokensPerTurn { get; init; }
    /// <summary>Reasoning tokens retained after each call; explicit because provider behavior varies.</summary>
    public long RetainedReasoningTokensPerTurn { get; init; }
    /// <summary>Uncached suffix on each call; retained and cacheable on later calls.</summary>
    public long InputTokensPerTurn { get; init; }
    /// <summary>Number of calls.</summary>
    public int Turns { get; init; }
    /// <summary>Effective cache lifetime; null is explicitly unknown, zero disables caching.</summary>
    public double? CacheTtlMinutes { get; init; }
    /// <summary>Default elapsed time between request starts.</summary>
    public double GapMinutes { get; init; } = 1;
    /// <summary>Optional per-call elapsed times. First entry must be zero, length must equal Turns.</summary>
    public IReadOnlyList<double>? GapsMinutes { get; init; }
    /// <summary>Cacheable minimum, explicitly supplied by provider policy or assumption.</summary>
    public long MinimumCacheTokens { get; init; }
    /// <summary>Tokens already warm before the first call.</summary>
    public long InitiallyCachedTokens { get; init; }
    /// <summary>Reset at each task boundary; zero keeps one session.</summary>
    public int RestartEveryTurns { get; init; }
    /// <summary>Context reconstructed at restart.</summary>
    public long RestartContext { get; init; }
    /// <summary>Stable prefix shared between fresh sessions; only reused while warm.</summary>
    public long SharedPrefixTokens { get; init; }
    /// <summary>Compact before a call at or above this context size; zero disables.</summary>
    public long CompactionThreshold { get; init; }
    /// <summary>Total context after compaction, including the summary.</summary>
    public long CompactedContext { get; init; }
    /// <summary>Summary output billed by the additional compaction call.</summary>
    public long CompactionOutputTokens { get; init; }
    /// <summary>Compaction call duration; a long duration may expire its cache before the next call.</summary>
    public double CompactionMinutes { get; init; }
    /// <summary>Explicit total cache-write rate override, e.g. the provider's one-hour tariff.</summary>
    public decimal? CacheWritePerMTokOverride { get; init; }
}

/// <summary>A call, including any preceding compaction iteration. Costs use disjoint token categories.</summary>
public sealed record ContextTurnCost(int Turn, DateTime AtUtc, long ContextBeforeCompaction,
    long ContextTokens, bool Compacted, bool CacheExpired, TokenUsage Usage,
    CostBreakdown Cost, CostBreakdown? CompactionCost, decimal? CumulativeCost);

/// <summary>Unknown assumptions/prices produce a null total and an explicit explanation.</summary>
public sealed record ContextSessionForecast(IReadOnlyList<ContextTurnCost> Turns, decimal? Total, string? UnavailableReason);

/// <summary>Pure session-cost recurrence; no provider calls, filesystem access, or routing changes.</summary>
public static class ContextSessionCost
{
    /// <summary>Forecast all calls, rewriting cache on expiry, restart, or compaction.</summary>
    public static ContextSessionForecast Forecast(ContextSessionOptions options, ModelPriceCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        if (options.CacheTtlMinutes is null)
            return new([], null, "Cache TTL is unknown; supply an effective lifetime or disable caching with zero.");
        catalog ??= ModelPriceCatalog.Default;
        var rows = new List<ContextTurnCost>();
        var context = options.InitialContext;
        var cached = options.InitiallyCachedTokens;
        var at = options.StartUtc;
        decimal? cumulative = 0;
        string? unavailable = null;
        for (var i = 0; i < options.Turns; i++)
        {
            var gap = i == 0 ? 0 : options.GapsMinutes?[i] ?? options.GapMinutes;
            at = at.AddMinutes(gap);
            var expired = i > 0 && gap >= options.CacheTtlMinutes;
            if (expired) cached = 0;
            if (i > 0 && options.RestartEveryTurns > 0 && i % options.RestartEveryTurns == 0)
            {
                context = options.RestartContext;
                cached = Math.Min(cached, options.SharedPrefixTokens);
            }
            var before = context;
            var compacted = options.CompactionThreshold > 0 && context >= options.CompactionThreshold;
            CostBreakdown? compaction = null;
            TokenUsage compactionUsage = default;
            if (compacted)
            {
                compactionUsage = Usage(context, cached, 0, options.CompactionOutputTokens, options);
                compaction = Price(compactionUsage, at, options, catalog);
                context = options.CompactedContext;
                // A changed prefix invalidates all cached history in this conservative model.
                cached = 0;
                at = at.AddMinutes(options.CompactionMinutes);
            }
            var usage = Usage(context, cached, options.InputTokensPerTurn,
                checked(options.OutputTokensPerTurn + options.ReasoningTokensPerTurn), options);
            var cost = Price(usage, at, options, catalog);
            cumulative = cumulative + cost.Total + (compaction is null ? 0 : compaction.Total);
            if (!cost.HasPrice || compaction is { HasPrice: false }) unavailable = "No usable USD tariff for one or more calls (unknown model, date, or cache rate).";
            rows.Add(new(i + 1, at, before, context, compacted, expired,
                new(checked(usage.Input + compactionUsage.Input), checked(usage.Output + compactionUsage.Output),
                    checked(usage.CacheRead + compactionUsage.CacheRead), checked(usage.CacheWrite + compactionUsage.CacheWrite)),
                cost, compaction, cumulative));
            cached = options.CacheTtlMinutes > 0 && context >= options.MinimumCacheTokens ? context : 0;
            context = checked(context + options.InputTokensPerTurn + options.ToolTokensPerTurn + options.OutputTokensPerTurn + options.RetainedReasoningTokensPerTurn);
        }
        return new(rows.AsReadOnly(), cumulative, unavailable);
    }

    /// <summary>First completed task at which long is strictly more expensive; null means none in the horizon or unknown.</summary>
    public static int? FirstMoreExpensiveTask(ContextSessionForecast longer, ContextSessionForecast fresh, int turnsPerTask)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(turnsPerTask, 1);
        for (var i = turnsPerTask - 1; i < Math.Min(longer.Turns.Count, fresh.Turns.Count); i += turnsPerTask)
            if (longer.Turns[i].CumulativeCost is { } a && fresh.Turns[i].CumulativeCost is { } b && a > b)
                return (i + 1) / turnsPerTask;
        return null;
    }

    /// <summary>Warm-cache payback bound, including summary rewrite; excludes quality and later growth.</summary>
    public static int? PaybackTurns(decimal upfrontCost, long discardedTokens, decimal cacheReadPerMTok)
    {
        if (upfrontCost < 0 || discardedTokens < 0 || cacheReadPerMTok < 0) throw new ArgumentOutOfRangeException(nameof(upfrontCost));
        return discardedTokens == 0 || cacheReadPerMTok == 0 ? null
            : checked((int)decimal.Ceiling(upfrontCost / (discardedTokens * cacheReadPerMTok / 1_000_000m)));
    }

    private static TokenUsage Usage(long context, long cached, long suffix, long output, ContextSessionOptions o)
    {
        if (o.CacheTtlMinutes == 0 || context < o.MinimumCacheTokens) return new(checked(context + suffix), output);
        var read = cached >= o.MinimumCacheTokens ? Math.Min(context, cached) : 0;
        return new(suffix, output, read, context - read);
    }

    private static CostBreakdown Price(TokenUsage usage, DateTime at, ContextSessionOptions o, ModelPriceCatalog catalog)
    {
        var cost = catalog.ComputeCost(o.ModelId, usage, at);
        if (cost.Price is { } rate && (rate.Currency != Currencies.Usd
            || usage.CacheRead > 0 && rate.CacheReadPerMTok is null
            || usage.CacheWrite > 0 && rate.CacheWritePerMTok is null && o.CacheWritePerMTokOverride is null))
            return new CostBreakdown { Status = PriceStatus.NoPriceForDate, ModelId = o.ModelId };
        if (cost.Price is not { } p || o.CacheWritePerMTokOverride is not { } write) return cost;
        var writeCost = usage.CacheWrite * write / 1_000_000m;
        return cost with { Price = p with { CacheWritePerMTok = write, Note = "Explicit session write-rate override" },
            CacheWriteCost = writeCost, Total = cost.Total - cost.CacheWriteCost + writeCost };
    }

    private static void Validate(ContextSessionOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.ModelId) || string.IsNullOrWhiteSpace(o.ReasoningLevel)) throw new ArgumentException("Model and reasoning label are required.");
        if (o.StartUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("StartUtc must be UTC.");
        if (o.Turns < 0 || o.Turns > 100_000 || o.RestartEveryTurns < 0) throw new ArgumentOutOfRangeException(nameof(o));
        long[] counts = [o.InitialContext, o.ToolTokensPerTurn, o.OutputTokensPerTurn, o.ReasoningTokensPerTurn,
            o.RetainedReasoningTokensPerTurn, o.InputTokensPerTurn, o.MinimumCacheTokens, o.InitiallyCachedTokens, o.RestartContext, o.SharedPrefixTokens,
            o.CompactionThreshold, o.CompactedContext, o.CompactionOutputTokens];
        if (counts.Any(n => n < 0) || o.CacheWritePerMTokOverride < 0) throw new ArgumentOutOfRangeException(nameof(o));
        if (!double.IsFinite(o.GapMinutes) || o.GapMinutes < 0 || !double.IsFinite(o.CompactionMinutes) || o.CompactionMinutes < 0
            || o.CacheTtlMinutes is { } ttl && (!double.IsFinite(ttl) || ttl < 0)) throw new ArgumentOutOfRangeException(nameof(o));
        if (o.RetainedReasoningTokensPerTurn > o.ReasoningTokensPerTurn || o.InitiallyCachedTokens > o.InitialContext || o.SharedPrefixTokens > o.RestartContext
            || o.CompactionThreshold > 0 && o.CompactedContext >= o.CompactionThreshold) throw new ArgumentException("Invalid prefix or compaction sizes.");
        if (o.GapsMinutes is { } gaps && (gaps.Count != o.Turns || gaps.Any(g => !double.IsFinite(g) || g < 0) || gaps.Count > 0 && gaps[0] != 0))
            throw new ArgumentException("Gaps must match turns and begin with zero.");
    }
}
