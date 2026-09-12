namespace TokenEconomy;

/// <summary>ISO 4217 currency codes used by the pricing catalog.</summary>
public static class Currencies
{
    /// <summary>United States dollar — the currency every seeded price is quoted in.</summary>
    public const string Usd = "USD";
}

/// <summary>
/// One historical price point for a model, in a single currency, effective from
/// <see cref="ValidFrom"/> (inclusive) through <see cref="ValidTo"/> (inclusive, when set). Rates are quoted
/// per one million tokens (per-MTok). The cost of a run is always computed with the entry that
/// was valid at the run's UTC timestamp. A genuine price change appends a later entry and preserves
/// the earlier period. Incorrect or officially cancelled future entries are corrected with provenance
/// rather than retained as tariffs that actually took effect.
/// </summary>
public sealed record ModelPrice
{
    /// <summary>Price per one million input (prompt) tokens.</summary>
    public required decimal InputPerMTok { get; init; }

    /// <summary>Price per one million output (completion) tokens. Providers that bill reasoning tokens do so at the output rate.</summary>
    public required decimal OutputPerMTok { get; init; }

    /// <summary>
    /// Price per one million cache-read (cache-hit) tokens, or null when the model has no separate
    /// cache-read rate. When null, <see cref="ModelPriceCatalog.ComputeCost(string, TokenUsage, DateTime)"/> falls back to the
    /// input rate for cache-read tokens rather than dropping them silently.
    /// </summary>
    public decimal? CacheReadPerMTok { get; init; }

    /// <summary>
    /// Price per one million cache-write (cache-creation) tokens, or null when the model has no
    /// separate cache-write rate. Seeded Anthropic entries use the 5-minute duration; OpenAI GPT-5.6+
    /// entries use the published 1.25x input rate for cache creation with at least 30-minute retention.
    /// This is the total rate for disjoint cache-write tokens, not a surcharge added to input tokens.
    /// When null, cache-write tokens are billed at the input rate (see <see cref="CacheReadPerMTok"/>).
    /// </summary>
    public decimal? CacheWritePerMTok { get; init; }

    /// <summary>ISO 4217 currency code the rates are quoted in. Defaults to <see cref="Currencies.Usd"/>.</summary>
    public string Currency { get; init; } = Currencies.Usd;

    /// <summary>
    /// UTC instant this price took effect, inclusive. A base entry whose real start date is unknown
    /// uses <see cref="DateTime.MinValue"/> — it then applies to any timestamp up to the next entry.
    /// Store as <see cref="DateTimeKind.Utc"/>; resolution compares this against the run's
    /// <c>atUtc</c> by instant.
    /// </summary>
    public DateTime ValidFrom { get; init; }

    /// <summary>
    /// UTC instant at which this price period ends, inclusive. Null means the period has no known
    /// end yet. Price changes should close the previous period and append a new entry rather than
    /// overwrite historical data.
    /// </summary>
    public DateTime? ValidTo { get; init; }

    /// <summary>Where the number came from (pricing page, release note, catalog import, …). Free text.</summary>
    public string? Source { get; init; }

    /// <summary>Primary source URLs supporting the tariff and its historical dates. Empty for legacy or host-supplied entries without structured provenance.</summary>
    public IReadOnlyList<string> SourceUrls { get; init; } = [];

    /// <summary>UTC calendar date on which the sources were checked; independent of when the price took effect.</summary>
    public DateOnly? VerifiedOn { get; init; }

    /// <summary>
    /// Basis for the effective date, e.g. <c>provider-announced-date</c> or <c>release-date-inference</c>.
    /// Provider calendar days are normalized to midnight UTC; exact intraday cutovers may be unpublished.
    /// Per-category historical evidence limits belong in <see cref="Note"/>.
    /// </summary>
    public string? ValidFromBasis { get; init; }

    /// <summary>Optional human note, e.g. <c>"Standard direct API; short-context requests."</c>.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// True when the number is a best-effort placeholder not yet confirmed against an authoritative
    /// source. The cost is still computed with it, but <see cref="CostBreakdown.Unconfirmed"/> is set
    /// so a caller can surface the caveat instead of trusting the figure silently. This flag describes
    /// the numerical tariff; an uncertain effective date is separately recorded by <see cref="ValidFromBasis"/>.
    /// </summary>
    public bool Unconfirmed { get; init; }

    /// <summary>The required consumer-facing caveat for catalog-derived list-price estimates.</summary>
    public const string EstimatedListPricesCaveat = "estimated - list prices";
}

/// <summary>
/// The pricing record for one model: its canonical id, any aliases that resolve to it, an optional
/// vendor grouping, and the full price <see cref="History"/> (entry order does not matter — resolution
/// selects by <see cref="ModelPrice.ValidFrom"/>). An empty history means the model is known but has no
/// confirmed price; a lookup then resolves to <see cref="PriceStatus.NoPriceForDate"/>, never a silent zero.
/// </summary>
public sealed record ModelListing
{
    /// <summary>Canonical model identifier (as passed to <c>--model &lt;id&gt;</c>).</summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Human-readable label for UI presentation (e.g. <c>"Claude Sonnet 5"</c>). Every listing in
    /// <see cref="ModelPriceCatalog.Default"/> carries one (enforced by
    /// <c>ModelPriceSeedTests.EveryListing_HasADisplayName</c>) so a consumer never has to fall back
    /// to a raw id, or maintain its own partial copy of this mapping. Null on host-supplied listings
    /// that do not need one (e.g. ad hoc test catalogs).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Provider-published initial release or preview date, independent of price validity and
    /// snapshot-id suffixes. Null means the publication date has not been verified.
    /// </summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>Primary source supporting <see cref="ReleaseDate"/>, or null when unknown.</summary>
    public string? ReleaseDateSource { get; init; }

    /// <summary>Alternate ids that resolve to this listing (dated snapshots, spelling variants, …). Matched case- and dot/dash-insensitively.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Optional vendor / family grouping (<c>anthropic</c>, <c>openai</c>, <c>google</c>, …).</summary>
    public string? Vendor { get; init; }

    /// <summary>The model's price history. May be empty when the model is known but not yet priced.</summary>
    public IReadOnlyList<ModelPrice> History { get; init; } = [];

    /// <summary>Optional note explaining the listing — e.g. that pricing is not yet published, or that a tier is deprecated.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Token counts for a single run or turn, as consumed by <see cref="ModelPriceCatalog.ComputeCost(string, TokenUsage, DateTime)"/>.
/// Every field is a raw non-negative token total (not per-MTok); negative values are treated as zero.
/// </summary>
/// <param name="Input">Fresh (non-cached) input tokens.</param>
/// <param name="Output">Generated output tokens (reasoning tokens are billed as output by the providers that report them).</param>
/// <param name="CacheRead">Cache-read (cache-hit) input tokens.</param>
/// <param name="CacheWrite">Cache-write (cache-creation) input tokens.</param>
public readonly record struct TokenUsage(long Input, long Output, long CacheRead = 0, long CacheWrite = 0);
