using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public data records mirror the documented effort-dial.schema.json.

/// <summary>The CLI a dial pick runs on.</summary>
public enum DialCli
{
    /// <summary>Claude Code (Anthropic models).</summary>
    Claude,

    /// <summary>Codex CLI (OpenAI models).</summary>
    Codex,
}

/// <summary>What the cost of a rung is expressed per.</summary>
public enum DialCostUnit
{
    /// <summary>Per accepted card, anchored on a fleet cohort.</summary>
    AcceptedCard,

    /// <summary>Per benchmark task (for example one AA-3D build); a proxy where no fleet cohort exists.</summary>
    BenchmarkTask,
}

/// <summary>Whether a rung has an expected cost.</summary>
public enum DialCostStatus
{
    /// <summary>A low/high list-price range was derived from an anchor and factors.</summary>
    Estimated,

    /// <summary>No anchor exists; the cost is unknown (never a silent zero).</summary>
    Unknown,
}

/// <summary>How a rung's cost maps onto the subscription allowance.</summary>
public enum DialQuotaBasis
{
    /// <summary>Codex: the official Standard credit rate card (list price times 25).</summary>
    CodexCreditRate,

    /// <summary>Codex model without a usable credit rate card.</summary>
    CodexCreditsUnavailable,

    /// <summary>Claude: no vendor publishes a per-model weight for the weekly limit; list price is a flagged proxy.</summary>
    ClaudeListPriceProxy,

    /// <summary>Claude: a measured weight (quota percentage points per accepted card) reported by Agent Studio.</summary>
    ClaudeMeasuredWeight,
}

public sealed record DialDecision
{
    public required DateOnly DecidedOn { get; init; }
    public required string Semantics { get; init; }
    public required string Summary { get; init; }
    public required EffortLevel StandardEffort { get; init; }
    public required EffortLevel HardWorkEffort { get; init; }
    public required string XHighPolicy { get; init; }
    public required string ReviewAspectPolicy { get; init; }
}

public sealed record DialPositions
{
    public required int Minimum { get; init; }
    public required int Maximum { get; init; }
    public required int Default { get; init; }
}

public sealed record DialEvidenceClass
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
}

public sealed record DialFleetRow
{
    public required string Id { get; init; }
    public required string ModelId { get; init; }
    public required EffortLevel Effort { get; init; }
    /// <summary>Case count of the row (cards); required for fleet evidence.</summary>
    public required int Cards { get; init; }
    public required int Accepted { get; init; }
    public int? FirstReviewPassed { get; init; }
    public int? FirstReviewed { get; init; }
    public required decimal MedianListUsdPerAcceptedCard { get; init; }
    public long? MedianTokensPerAcceptedCard { get; init; }
}

public sealed record DialCacheReadObservation
{
    public required string Id { get; init; }
    public required string ModelId { get; init; }
    public required EffortLevel Effort { get; init; }
    public long? CachedReadTokens { get; init; }
    public decimal? CacheReadShareOfListCost { get; init; }
    public required string Note { get; init; }
}

/// <summary>An anonymised fleet cohort (evidence class F).</summary>
public sealed record DialFleetCohort
{
    public required string Id { get; init; }
    public required string EvidenceClass { get; init; }
    public required DateOnly WindowStart { get; init; }
    public required DateOnly WindowEnd { get; init; }
    public required string Population { get; init; }
    public decimal? PureCostShareThreshold { get; init; }
    public required string PriceBasis { get; init; }
    public decimal? RetryTaxShareOfSpend { get; init; }
    public decimal? TotalListSpendUsd { get; init; }
    public decimal? CodingShareOfSpend { get; init; }
    public required IReadOnlyList<string> Caveats { get; init; }
    public required IReadOnlyList<DialFleetRow> Rows { get; init; }
    public IReadOnlyList<DialCacheReadObservation> CacheReadObservations { get; init; } = [];
}

public sealed record DialEvidence
{
    public required string Id { get; init; }
    public required string EvidenceClass { get; init; }
    public required string Scope { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<string> BenchmarkResultIds { get; init; } = [];
    public IReadOnlyList<string> FleetRowIds { get; init; } = [];
    public IReadOnlyList<string> SubscriptionObservationIds { get; init; } = [];
    public IReadOnlyList<string> SourceUrls { get; init; } = [];
    public DateOnly? RetrievedAt { get; init; }
    public required string Note { get; init; }
}

/// <summary>A conclusion with the work classes it applies to, so it is never carried over silently.</summary>
public sealed record DialConclusion
{
    public required string Id { get; init; }
    public required string Statement { get; init; }
    public required IReadOnlyList<string> AppliesTo { get; init; }
    public required IReadOnlyList<string> ProvisionalFor { get; init; }
    public required IReadOnlyList<string> DoesNotApplyTo { get; init; }
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

public sealed record DialRubricDimension
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Question { get; init; }
    public required string GameExample { get; init; }
}

public sealed record DialBenchmarkDimensions
{
    public required string Originality { get; init; }
    public required string Design { get; init; }
    public required string GameFeel { get; init; }
    public required string TechnicalExecution { get; init; }
}

public sealed record DialCreativeBenchmark
{
    public required string BenchmarkTypeId { get; init; }
    public required string Method { get; init; }
    public required string Judged { get; init; }
    public required string Coverage { get; init; }
    public required DialBenchmarkDimensions Dimensions { get; init; }
    public bool? ExcludesBrokenBuildsBeforeVote { get; init; }
    public required bool PublishesNonRunningShare { get; init; }
    public required string PublishesCost { get; init; }
    public required string Notes { get; init; }
    public required IReadOnlyList<string> Gaps { get; init; }
}

public sealed record DialCreativeRubric
{
    public required IReadOnlyList<DialRubricDimension> Dimensions { get; init; }
    public required IReadOnlyList<DialCreativeBenchmark> Benchmarks { get; init; }
}

public sealed record DialRepriceRef
{
    public required string FromModelId { get; init; }
    public required string ToModelId { get; init; }
    public decimal? CacheReadShare { get; init; }
    public string? CacheReadObservationId { get; init; }
}

/// <summary>One multiplicative term of a cost factor bound.</summary>
public sealed record DialRatioRef
{
    public IReadOnlyList<string>? BenchmarkRatio { get; init; }
    public IReadOnlyList<string>? FleetRatio { get; init; }
    public DialRepriceRef? Reprice { get; init; }
    public decimal? Constant { get; init; }
    public string? Reason { get; init; }
}

public sealed record DialCostFactor
{
    public required IReadOnlyList<DialRatioRef> Low { get; init; }
    public required IReadOnlyList<DialRatioRef> High { get; init; }
    public required string Note { get; init; }
}

public sealed record DialCostAnchor
{
    public string? FleetRowId { get; init; }
    public string? BenchmarkResultId { get; init; }
}

public sealed record DialExpectedCost
{
    public required DialCostAnchor Anchor { get; init; }
    public required IReadOnlyList<DialCostFactor> Factors { get; init; }
}

/// <summary>Slot for a measured Claude weekly-limit weight, reported later by Agent Studio.</summary>
public sealed record DialMeasuredQuotaWeight
{
    public required decimal PercentagePointsPerAcceptedCard { get; init; }
    public required DateOnly WindowStart { get; init; }
    public required DateOnly WindowEnd { get; init; }
    public required int CaseCount { get; init; }
    public required string PlanId { get; init; }
}

public sealed record DialRungDimension
{
    public required string Status { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

/// <summary>Per creative-rubric dimension, what the evidence says for one rung.</summary>
public sealed record DialRungDimensions
{
    public required DialRungDimension Originality { get; init; }
    public required DialRungDimension Design { get; init; }
    public required DialRungDimension GameFeel { get; init; }
    public required DialRungDimension TechnicalExecution { get; init; }
}

/// <summary>One (model, reasoning level) step of a ladder.</summary>
public sealed record DialRung
{
    public required string Id { get; init; }
    public required string ModelId { get; init; }
    public required EffortLevel Effort { get; init; }
    /// <summary>Start of this rung's slider band; null for an escalation-only rung.</summary>
    public int? FromPosition { get; init; }
    /// <summary>Never picked by position; reachable only through <see cref="EffortDial.Escalate"/>.</summary>
    public bool EscalationOnly { get; init; }
    public string? Restriction { get; init; }
    public required string Confidence { get; init; }
    public required IReadOnlyList<string> EvidenceIds { get; init; }
    public DialExpectedCost? ExpectedCost { get; init; }
    public string? CostUnknownReason { get; init; }
    public DialMeasuredQuotaWeight? MeasuredQuotaWeight { get; init; }
    public DialRungDimensions? Dimensions { get; init; }
    public string? Note { get; init; }
}

/// <summary>A configuration recorded with its evidence but never picked by the dial.</summary>
public sealed record DialCandidate
{
    public required string ModelId { get; init; }
    public required EffortLevel Effort { get; init; }
    public required IReadOnlyList<string> EvidenceIds { get; init; }
    public required string Reason { get; init; }
}

public sealed record DialLadder
{
    public required IReadOnlyList<DialRung> Rungs { get; init; }
    public required IReadOnlyList<DialCandidate> Candidates { get; init; }
}

public sealed record DialLadders
{
    public required DialLadder Claude { get; init; }
    public required DialLadder Codex { get; init; }
}

/// <summary>A minimum rung the dial never goes below; conditional floors apply only when the caller passes the condition.</summary>
public sealed record DialFloor
{
    public required string Cli { get; init; }
    public string? Condition { get; init; }
    public required string RungId { get; init; }
    public required string Reason { get; init; }
}

public sealed record DialWorkClass
{
    public required string Id { get; init; }
    public required string Letter { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public required string EvidenceBasis { get; init; }
    public required bool AllowLightTier { get; init; }
    public required string Confidence { get; init; }
    public required IReadOnlyList<DialFloor> Floors { get; init; }
    public required IReadOnlyList<string> EvidenceGaps { get; init; }
    public required DialLadders Ladders { get; init; }

    /// <summary>The ladder for one CLI.</summary>
    public DialLadder Ladder(DialCli cli) => cli == DialCli.Claude ? Ladders.Claude : Ladders.Codex;
}

/// <summary>Expected cost of one rung: a list-price range plus the subscription view.</summary>
public sealed record DialRungCost
{
    public required DialCostStatus Status { get; init; }
    public DialCostUnit? Unit { get; init; }
    public decimal? ListUsdLow { get; init; }
    public decimal? ListUsdHigh { get; init; }
    /// <summary>Codex Standard credits; null for Claude rungs and for Codex models without a rate card.</summary>
    public decimal? CodexCreditsLow { get; init; }
    public decimal? CodexCreditsHigh { get; init; }
    public required DialQuotaBasis QuotaBasis { get; init; }
    public decimal? MeasuredQuotaPointsPerAcceptedCard { get; init; }
    public required IReadOnlyList<string> Derivation { get; init; }
    public string? UnknownReason { get; init; }
}

/// <summary>What the dial runs for one work class and CLI at one position.</summary>
public sealed record DialPick
{
    public required int Position { get; init; }
    public required string WorkClassId { get; init; }
    public required DialCli Cli { get; init; }
    public required DialRung Rung { get; init; }
    public required int RungIndex { get; init; }
    /// <summary>The rung the band alone selects, before floors and routing restrictions.</summary>
    public required string BandRungId { get; init; }
    public DialFloor? AppliedFloor { get; init; }
    public required IReadOnlyList<string> SkippedRestrictedRungIds { get; init; }
    public required DialRungCost Cost { get; init; }
}

internal sealed record EffortDialDocument
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateOnly AsOfDate { get; init; }
    public required DialDecision Decision { get; init; }
    public required DialPositions Positions { get; init; }
    public required IReadOnlyList<DialEvidenceClass> EvidenceClasses { get; init; }
    public required IReadOnlyList<DialFleetCohort> FleetCohorts { get; init; }
    public required IReadOnlyList<DialEvidence> Evidence { get; init; }
    public required IReadOnlyList<DialConclusion> Conclusions { get; init; }
    public required DialCreativeRubric CreativeRubric { get; init; }
    public required IReadOnlyList<DialWorkClass> WorkClasses { get; init; }
}

/// <summary>
/// The 'frugal to strong' effort dial. A slider position (0..100) picks, per work class and CLI, one rung
/// (model and reasoning level) of an ordered ladder; floors set a minimum rung; restricted models are
/// skipped downwards. Deterministic and pure over the embedded catalogue; see docs/effort-dial.md.
/// </summary>
public sealed partial class EffortDial
{
    private const string Resource = "TokenEconomy.catalog.effort-dial.json";
    private static readonly Lazy<EffortDial> DefaultDial = new(LoadDefault);
    private readonly Dictionary<string, DialWorkClass> _classes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialRungCost> _costs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialFleetRow> _fleetRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialCacheReadObservation> _cacheObservations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialEvidence> _evidence = new(StringComparer.Ordinal);
    private readonly ModelPriceCatalog _prices;
    private readonly ModelRoutingKnowledgeBase _routing;
    private readonly IReadOnlyList<BenchmarkEvidenceCatalog> _benchmarks;
    private readonly SubscriptionCatalogue _subscriptions;

    internal EffortDial(EffortDialDocument document, ModelPriceCatalog prices, ModelRoutingKnowledgeBase routing,
        IReadOnlyList<BenchmarkEvidenceCatalog> benchmarks, SubscriptionCatalogue subscriptions)
    {
        ArgumentNullException.ThrowIfNull(document);
        _prices = prices;
        _routing = routing;
        _benchmarks = benchmarks;
        _subscriptions = subscriptions;
        if (document.Schema != "effort-dial.schema.json" || document.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported effort dial schema version.");
        AsOfDate = document.AsOfDate;
        Decision = document.Decision;
        Positions = document.Positions;
        EvidenceClassDefinitions = document.EvidenceClasses;
        FleetCohorts = document.FleetCohorts;
        Evidence = document.Evidence;
        Conclusions = document.Conclusions;
        CreativeRubric = document.CreativeRubric;
        WorkClasses = document.WorkClasses;
        Validate();
    }

    /// <summary>The embedded catalogue, validated against the default price, routing, benchmark and subscription catalogues.</summary>
    public static EffortDial Default => DefaultDial.Value;

    public DateOnly AsOfDate { get; }
    public DialDecision Decision { get; }
    public DialPositions Positions { get; }
    /// <summary>The default slider position; at it every class picks its standard rung.</summary>
    public int DefaultPosition => Positions.Default;
    public IReadOnlyList<DialEvidenceClass> EvidenceClassDefinitions { get; }
    public IReadOnlyList<DialFleetCohort> FleetCohorts { get; }
    public IReadOnlyList<DialEvidence> Evidence { get; }
    public IReadOnlyList<DialConclusion> Conclusions { get; }
    public DialCreativeRubric CreativeRubric { get; }
    public IReadOnlyList<DialWorkClass> WorkClasses { get; }

    /// <summary>Find a work class by id; null when unknown.</summary>
    public DialWorkClass? FindWorkClass(string? id) => id is null ? null : _classes.GetValueOrDefault(id);

    /// <summary>Find an evidence entry by id; null when unknown.</summary>
    public DialEvidence? FindEvidence(string? id) => id is null ? null : _evidence.GetValueOrDefault(id);

    /// <summary>Find a fleet cohort row by id; null when unknown.</summary>
    public DialFleetRow? FindFleetRow(string? id) => id is null ? null : _fleetRows.GetValueOrDefault(id);

    /// <summary>
    /// The rung the dial runs at <paramref name="position"/> for one work class and CLI. The band picks the last
    /// rung whose <see cref="DialRung.FromPosition"/> is at or below the position; applicable floors raise it;
    /// a rung whose model is not selectable in the routing policy is skipped downwards, never below the floor.
    /// </summary>
    /// <param name="position">Slider position, 0 (frugal) to 100 (strong).</param>
    /// <param name="workClassId">Work class id, for example <c>feature-logic</c>.</param>
    /// <param name="cli">The CLI the card runs on.</param>
    /// <param name="conditions">Conditions that activate conditional floors, for example <c>security-relevant</c>.</param>
    public DialPick Pick(int position, string workClassId, DialCli cli, IEnumerable<string>? conditions = null)
    {
        if (position < Positions.Minimum || position > Positions.Maximum)
            throw new ArgumentOutOfRangeException(nameof(position), position, "The dial position must be between 0 and 100.");
        var workClass = FindWorkClass(workClassId) ?? throw new ArgumentException($"Unknown work class '{workClassId}'.", nameof(workClassId));
        var active = conditions?.ToHashSet(StringComparer.Ordinal) ?? [];
        var rungs = workClass.Ladder(cli).Rungs;
        var bandIndex = BandIndex(rungs, position);
        var floors = workClass.Floors.Where(floor => floor.Cli == CliKey(cli) && (floor.Condition is null || active.Contains(floor.Condition)))
            .Select(floor => (Floor: floor, Index: IndexOf(rungs, floor.RungId))).ToArray();
        var floorIndex = floors.Length == 0 ? 0 : floors.Max(floor => floor.Index);
        var index = Math.Max(bandIndex, floorIndex);
        var skipped = new List<string>();
        while (!Selectable(rungs[index]) && index > floorIndex)
        {
            skipped.Add(rungs[index].Id);
            index--;
        }
        if (!Selectable(rungs[index]))
            throw new InvalidOperationException($"No selectable rung at or above the floor in '{workClassId}' ({cli}).");
        var applied = floors.Length > 0 && floorIndex > bandIndex
            ? floors.Where(floor => floor.Index == floorIndex).Select(floor => floor.Floor).First()
            : null;
        return new DialPick
        {
            Position = position,
            WorkClassId = workClass.Id,
            Cli = cli,
            Rung = rungs[index],
            RungIndex = index,
            BandRungId = rungs[bandIndex].Id,
            AppliedFloor = applied,
            SkippedRestrictedRungIds = skipped,
            Cost = _costs[rungs[index].Id],
        };
    }

    /// <summary>The inverse view: what runs at <paramref name="position"/> for every work class on one CLI.</summary>
    public IReadOnlyList<DialPick> PicksAt(int position, DialCli cli, IEnumerable<string>? conditions = null)
    {
        var active = conditions?.ToArray();
        return WorkClasses.Select(workClass => Pick(position, workClass.Id, cli, active)).ToArray();
    }

    /// <summary>Expected cost per accepted card (or per benchmark task, see <see cref="DialRungCost.Unit"/>) per work class at a position.</summary>
    public IReadOnlyDictionary<string, DialRungCost> ExpectedCostsAt(int position, DialCli cli, IEnumerable<string>? conditions = null) =>
        PicksAt(position, cli, conditions).ToDictionary(pick => pick.WorkClassId, pick => pick.Cost, StringComparer.Ordinal);

    /// <summary>The expected cost of any rung, including escalation-only rungs.</summary>
    public DialRungCost CostOf(string rungId) =>
        _costs.TryGetValue(rungId, out var cost) ? cost : throw new ArgumentException($"Unknown rung '{rungId}'.", nameof(rungId));

    /// <summary>
    /// The next stronger rung after <paramref name="rungId"/> when a card failed at it, including escalation-only
    /// rungs; null at the top. The result may be restricted in routing; check <see cref="IsSelectable"/>.
    /// </summary>
    public DialRung? Escalate(string workClassId, DialCli cli, string rungId)
    {
        var workClass = FindWorkClass(workClassId) ?? throw new ArgumentException($"Unknown work class '{workClassId}'.", nameof(workClassId));
        var rungs = workClass.Ladder(cli).Rungs;
        var index = IndexOf(rungs, rungId);
        if (index < 0)
            throw new ArgumentException($"Rung '{rungId}' is not on the {cli} ladder of '{workClassId}'.", nameof(rungId));
        return index + 1 < rungs.Count ? rungs[index + 1] : null;
    }

    /// <summary>True when the rung's model is selectable in the routing policy.</summary>
    public bool IsSelectable(DialRung rung) => Selectable(rung);

    /// <summary>Conclusions that apply (fully or provisionally) to one work class.</summary>
    public IReadOnlyList<DialConclusion> ConclusionsFor(string workClassId) =>
        Conclusions.Where(c => c.AppliesTo.Contains(workClassId) || c.ProvisionalFor.Contains(workClassId)).ToArray();

    internal static string CliKey(DialCli cli) => cli == DialCli.Claude ? "claude" : "codex";

    private static string RoutingCliId(DialCli cli) => cli == DialCli.Claude ? "claude-code" : "codex";

    internal static string EffortKey(EffortLevel effort) => effort.ToString().ToLowerInvariant();

    private bool Selectable(DialRung rung) => _routing.FindModel(rung.ModelId)?.RoutingStatus == ModelRoutingStatus.Selectable;

    private static int BandIndex(IReadOnlyList<DialRung> rungs, int position)
    {
        var index = 0;
        for (var i = 0; i < rungs.Count; i++)
        {
            if (rungs[i].FromPosition is { } from && from <= position)
                index = i;
        }
        return index;
    }

    private static int IndexOf(IReadOnlyList<DialRung> rungs, string rungId)
    {
        for (var i = 0; i < rungs.Count; i++)
        {
            if (rungs[i].Id == rungId)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Parse and validate an effort-dial document (same schema as the embedded catalogue) against the default
    /// price, routing, benchmark and subscription catalogues. Throws <see cref="InvalidOperationException"/>
    /// when a semantic rule is broken.
    /// </summary>
    public static EffortDial Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<EffortDialDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("The effort dial document is empty.");
        return FromDocument(document);
    }

    /// <summary>The embedded catalogue as JSON text, for callers that derive a variant and re-validate it.</summary>
    public static string EmbeddedJson()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Embedded effort dial '{Resource}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static EffortDial LoadDefault() => Parse(EmbeddedJson());

    private static EffortDial FromDocument(EffortDialDocument document) =>
        new(document, ModelPriceCatalog.Default, ModelRoutingKnowledgeBase.PolicyOnly,
            [BenchmarkEvidenceCatalog.Default, BenchmarkEvidenceCatalog.Creative], SubscriptionCatalogue.Default);

    [GeneratedRegex(@"\b(?!GPT-)[A-Z][A-Z0-9]{1,9}-\d+\b")]
    private static partial Regex CardKeyPattern();

    private BenchmarkResult? FindBenchmarkResult(string id) =>
        _benchmarks.Select(catalog => catalog.FindResult(id)).FirstOrDefault(result => result is not null);

    private void Validate()
    {
        if (Positions.Minimum != 0 || Positions.Maximum != 100 || Positions.Default is < 0 or > 100)
            throw new InvalidOperationException("Dial positions must span 0..100 with a default inside the range.");
        if (Decision.Semantics != "frugalToStrong")
            throw new InvalidOperationException("Only the 'frugal to strong' dial semantics is defined.");
        if (!EvidenceClassDefinitions.Select(c => c.Id).SequenceEqual(EvidenceClasses.All))
            throw new InvalidOperationException("The dial must define exactly the evidence classes M, V, L, P and F in order.");
        ValidateFleet();
        ValidateEvidence();
        foreach (var workClass in WorkClasses)
        {
            if (!_classes.TryAdd(workClass.Id, workClass))
                throw new InvalidOperationException($"Duplicate work class '{workClass.Id}'.");
        }
        var rungIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workClass in WorkClasses)
            ValidateWorkClass(workClass, rungIds);
        ValidateConclusions();
        ValidateCreativeRubric();
    }

    private void ValidateFleet()
    {
        foreach (var cohort in FleetCohorts)
        {
            if (cohort.EvidenceClass != EvidenceClasses.Fleet)
                throw new InvalidOperationException($"Fleet cohort '{cohort.Id}' must carry evidence class F.");
            if (cohort.WindowStart == default || cohort.WindowEnd <= cohort.WindowStart)
                throw new InvalidOperationException($"Fleet cohort '{cohort.Id}' needs a measurement window.");
            if (cohort.Rows.Count == 0)
                throw new InvalidOperationException($"Fleet cohort '{cohort.Id}' has no rows.");
            var texts = new[] { cohort.Id, cohort.Population, cohort.PriceBasis }.Concat(cohort.Caveats)
                .Concat(cohort.Rows.Select(row => row.Id)).Concat(cohort.CacheReadObservations.SelectMany(o => new[] { o.Id, o.Note }));
            if (texts.FirstOrDefault(text => CardKeyPattern().IsMatch(text)) is { } leaked)
                throw new InvalidOperationException($"Fleet evidence must be anonymised; found a card key in '{leaked}'.");
            foreach (var row in cohort.Rows)
            {
                if (row.Cards <= 0 || row.Accepted < 0 || row.Accepted > row.Cards
                    || row.FirstReviewed > row.Cards || row.FirstReviewPassed > (row.FirstReviewed ?? row.Cards)
                    || row.MedianListUsdPerAcceptedCard <= 0)
                    throw new InvalidOperationException($"Fleet row '{row.Id}' has inconsistent counts or cost.");
                RequireModel(row.ModelId, $"fleet row '{row.Id}'");
                if (!_fleetRows.TryAdd(row.Id, row))
                    throw new InvalidOperationException($"Duplicate fleet row '{row.Id}'.");
            }
            foreach (var observation in cohort.CacheReadObservations)
            {
                if (observation.CacheReadShareOfListCost is < 0 or > 1)
                    throw new InvalidOperationException($"Cache-read share of '{observation.Id}' must be a fraction.");
                if (!_cacheObservations.TryAdd(observation.Id, observation))
                    throw new InvalidOperationException($"Duplicate cache-read observation '{observation.Id}'.");
            }
        }
    }

    private void ValidateEvidence()
    {
        foreach (var evidence in Evidence)
        {
            if (!EvidenceClasses.IsKnown(evidence.EvidenceClass))
                throw new InvalidOperationException($"Evidence '{evidence.Id}' has an unknown class.");
            if (!_evidence.TryAdd(evidence.Id, evidence))
                throw new InvalidOperationException($"Duplicate evidence '{evidence.Id}'.");
            if ((evidence.EvidenceClass == EvidenceClasses.Fleet) != (evidence.Scope == "fleet")
                || (evidence.EvidenceClass == EvidenceClasses.Fleet && evidence.FleetRowIds.Count == 0))
                throw new InvalidOperationException($"Fleet evidence '{evidence.Id}' must be class F, scope fleet, with fleet rows.");
            if (evidence.BenchmarkResultIds.Count + evidence.FleetRowIds.Count + evidence.SubscriptionObservationIds.Count + evidence.SourceUrls.Count == 0)
                throw new InvalidOperationException($"Evidence '{evidence.Id}' references nothing.");
            foreach (var id in evidence.BenchmarkResultIds)
            {
                var result = FindBenchmarkResult(id) ?? throw new InvalidOperationException($"Evidence '{evidence.Id}' cites unknown benchmark result '{id}'.");
                var creative = BenchmarkEvidenceCatalog.Creative.FindResult(id) is not null;
                if (creative != (evidence.Scope == "creative"))
                    throw new InvalidOperationException($"Evidence '{evidence.Id}' mixes creative and non-creative benchmark rows ('{result.Id}').");
            }
            if (evidence.FleetRowIds.FirstOrDefault(id => !_fleetRows.ContainsKey(id)) is { } row)
                throw new InvalidOperationException($"Evidence '{evidence.Id}' cites unknown fleet row '{row}'.");
            if (evidence.SubscriptionObservationIds.FirstOrDefault(id => _subscriptions.Observations.All(o => o.Id != id)) is { } observation)
                throw new InvalidOperationException($"Evidence '{evidence.Id}' cites unknown subscription observation '{observation}'.");
        }
    }

    private void ValidateWorkClass(DialWorkClass workClass, HashSet<string> rungIds)
    {
        foreach (var cli in new[] { DialCli.Claude, DialCli.Codex })
        {
            var ladder = workClass.Ladder(cli);
            var prefix = $"{workClass.Id}.{CliKey(cli)}.";
            if (ladder.Rungs.Count == 0)
                throw new InvalidOperationException($"'{workClass.Id}' has an empty {cli} ladder.");
            var previousFrom = -1;
            var seenEscalation = false;
            for (var i = 0; i < ladder.Rungs.Count; i++)
            {
                var rung = ladder.Rungs[i];
                if (!rung.Id.StartsWith(prefix, StringComparison.Ordinal) || !rungIds.Add(rung.Id))
                    throw new InvalidOperationException($"Rung id '{rung.Id}' must be unique and start with '{prefix}'.");
                ValidateRungModel(workClass, cli, rung);
                if (rung.EscalationOnly)
                {
                    if (rung.FromPosition is not null || i == 0)
                        throw new InvalidOperationException($"Escalation-only rung '{rung.Id}' has no band and cannot be the first rung.");
                    seenEscalation = true;
                }
                else
                {
                    if (seenEscalation || rung.FromPosition is not { } from || from <= previousFrom || (i == 0 && from != 0))
                        throw new InvalidOperationException($"Banded rungs of '{prefix}' must start at 0, rise strictly and precede escalation-only rungs ('{rung.Id}').");
                    previousFrom = from;
                }
                if (i > 0)
                    ValidateOrder(ladder.Rungs[i - 1], rung);
                foreach (var id in rung.EvidenceIds)
                {
                    var evidence = FindEvidence(id) ?? throw new InvalidOperationException($"Rung '{rung.Id}' cites unknown evidence '{id}'.");
                    if (workClass.EvidenceBasis == "creative" && evidence.Scope is not ("creative" or "fleet"))
                        throw new InvalidOperationException($"Creative rung '{rung.Id}' cites non-creative quality evidence '{id}'.");
                }
                ValidateDimensions(workClass, rung);
                if (cli == DialCli.Codex && rung.MeasuredQuotaWeight is not null)
                    throw new InvalidOperationException($"Codex rung '{rung.Id}' uses credits; a measured quota weight is a Claude slot.");
                _costs[rung.Id] = ComputeCost(cli, rung);
            }
            if (!Selectable(ladder.Rungs[0]))
                throw new InvalidOperationException($"The first rung of '{prefix}' must be selectable.");
            foreach (var candidate in ladder.Candidates)
            {
                RequireModel(candidate.ModelId, $"candidate of '{prefix}'");
                if (candidate.EvidenceIds.FirstOrDefault(id => FindEvidence(id) is null) is { } missing)
                    throw new InvalidOperationException($"Candidate of '{prefix}' cites unknown evidence '{missing}'.");
                if (ladder.Rungs.Any(r => r.ModelId == candidate.ModelId && r.Effort == candidate.Effort))
                    throw new InvalidOperationException($"'{candidate.ModelId}' {candidate.Effort} is both a rung and a candidate in '{prefix}'.");
            }
        }
        foreach (var floor in workClass.Floors)
        {
            var cli = floor.Cli == "claude" ? DialCli.Claude : floor.Cli == "codex" ? DialCli.Codex
                : throw new InvalidOperationException($"Floor of '{workClass.Id}' names unknown CLI '{floor.Cli}'.");
            var rung = workClass.Ladder(cli).Rungs.FirstOrDefault(r => r.Id == floor.RungId)
                ?? throw new InvalidOperationException($"Floor of '{workClass.Id}' names unknown rung '{floor.RungId}'.");
            if (rung.EscalationOnly || !Selectable(rung))
                throw new InvalidOperationException($"Floor rung '{rung.Id}' must be banded and selectable.");
        }
    }

    private void ValidateRungModel(DialWorkClass workClass, DialCli cli, DialRung rung)
    {
        RequireModel(rung.ModelId, $"rung '{rung.Id}'");
        var model = _routing.FindModel(rung.ModelId) ?? throw new InvalidOperationException($"Rung '{rung.Id}' model is not in the routing policy.");
        if (model.CliId != RoutingCliId(cli))
            throw new InvalidOperationException($"Rung '{rung.Id}' model runs on '{model.CliId}', not on the {cli} ladder.");
        if (!model.SupportedThinkingLevels.Contains(EffortKey(rung.Effort), StringComparer.Ordinal))
            throw new InvalidOperationException($"Rung '{rung.Id}' uses an effort the model does not support.");
        if (model.CapabilityTier == CapabilityTier.Light && !workClass.AllowLightTier)
            throw new InvalidOperationException($"Light-tier model in '{rung.Id}': Haiku and Luna class models are limited to mechanical and summary work.");
        if (rung.Confidence is not ("low" or "medium" or "high") || rung.EvidenceIds.Count == 0)
            throw new InvalidOperationException($"Rung '{rung.Id}' needs a confidence and at least one evidence reference.");
    }

    private void ValidateOrder(DialRung previous, DialRung rung)
    {
        var before = _routing.FindModel(previous.ModelId)!.CapabilityTier;
        var after = _routing.FindModel(rung.ModelId)!.CapabilityTier;
        if (after < before)
            throw new InvalidOperationException($"Ladder steps down in capability tier at '{rung.Id}'.");
        if (previous.ModelId == rung.ModelId && rung.Effort <= previous.Effort)
            throw new InvalidOperationException($"Ladder must raise the effort when it keeps the model at '{rung.Id}'.");
    }

    private void ValidateDimensions(DialWorkClass workClass, DialRung rung)
    {
        if (workClass.EvidenceBasis != "creative")
        {
            if (rung.Dimensions is not null)
                throw new InvalidOperationException($"Only creative rungs carry rubric dimensions ('{rung.Id}').");
            return;
        }
        var dimensions = rung.Dimensions ?? throw new InvalidOperationException($"Creative rung '{rung.Id}' must state all four rubric dimensions.");
        foreach (var dimension in new[] { dimensions.Originality, dimensions.Design, dimensions.GameFeel, dimensions.TechnicalExecution })
        {
            if (dimension.Status is not ("evidence" or "partial" or "none") || string.IsNullOrWhiteSpace(dimension.Text)
                || (dimension.Status == "none") != (dimension.EvidenceIds.Count == 0))
                throw new InvalidOperationException($"Creative rung '{rung.Id}' has an inconsistent dimension entry.");
            if (dimension.EvidenceIds.FirstOrDefault(id => FindEvidence(id)?.Scope != "creative") is { } bad)
                throw new InvalidOperationException($"Creative rung '{rung.Id}' dimension cites non-creative evidence '{bad}'.");
        }
    }

    private void ValidateConclusions()
    {
        foreach (var conclusion in Conclusions)
        {
            var named = conclusion.AppliesTo.Concat(conclusion.ProvisionalFor).Concat(conclusion.DoesNotApplyTo).ToArray();
            if (named.FirstOrDefault(id => !_classes.ContainsKey(id)) is { } unknown)
                throw new InvalidOperationException($"Conclusion '{conclusion.Id}' names unknown work class '{unknown}'.");
            if (named.Length != named.Distinct(StringComparer.Ordinal).Count())
                throw new InvalidOperationException($"Conclusion '{conclusion.Id}' lists a work class twice.");
            if (conclusion.EvidenceIds.FirstOrDefault(id => FindEvidence(id) is null) is { } missing)
                throw new InvalidOperationException($"Conclusion '{conclusion.Id}' cites unknown evidence '{missing}'.");
        }
    }

    private void ValidateCreativeRubric()
    {
        if (!CreativeRubric.Dimensions.Select(d => d.Id).SequenceEqual(["originality", "design", "gameFeel", "technicalExecution"]))
            throw new InvalidOperationException("The creative rubric has exactly the four owner dimensions in order.");
        foreach (var benchmark in CreativeRubric.Benchmarks)
        {
            var type = BenchmarkEvidenceCatalog.Creative.FindType(benchmark.BenchmarkTypeId)
                ?? throw new InvalidOperationException($"Creative rubric cites unknown benchmark '{benchmark.BenchmarkTypeId}'.");
            if (type.CapabilityClass != BenchmarkCapabilityClass.CreativeBuild)
                throw new InvalidOperationException($"Creative rubric benchmark '{type.Id}' is not a creative-build benchmark.");
            if (benchmark.ExcludesBrokenBuildsBeforeVote == true && benchmark.Dimensions.TechnicalExecution != "excluded")
                throw new InvalidOperationException($"'{type.Id}' filters broken builds before voting, so its technical execution is 'excluded'.");
        }
    }

    private void RequireModel(string modelId, string owner)
    {
        if (_prices.Find(modelId) is null)
            throw new InvalidOperationException($"Unknown model '{modelId}' in {owner}.");
    }

    private DialRungCost ComputeCost(DialCli cli, DialRung rung)
    {
        var claudeMeasured = cli == DialCli.Claude && rung.MeasuredQuotaWeight is not null;
        if (rung.ExpectedCost is null)
        {
            if (string.IsNullOrWhiteSpace(rung.CostUnknownReason))
                throw new InvalidOperationException($"Rung '{rung.Id}' without an expected cost must say why.");
            return new DialRungCost
            {
                Status = DialCostStatus.Unknown,
                QuotaBasis = QuotaBasisFor(cli, cli == DialCli.Codex ? CreditFactor(rung.ModelId) : null, claudeMeasured),
                MeasuredQuotaPointsPerAcceptedCard = rung.MeasuredQuotaWeight?.PercentagePointsPerAcceptedCard,
                Derivation = [],
                UnknownReason = rung.CostUnknownReason,
            };
        }
        var (anchorUsd, anchorModel, unit, anchorText) = ResolveAnchor(rung);
        var low = anchorUsd;
        var high = anchorUsd;
        var derivation = new List<string> { anchorText };
        foreach (var factor in rung.ExpectedCost.Factors)
        {
            var factorLow = factor.Low.Aggregate(1m, (product, term) => product * Term(term, rung, anchorModel));
            var factorHigh = factor.High.Aggregate(1m, (product, term) => product * Term(term, rung, anchorModel));
            if (factorLow <= 0 || factorHigh < factorLow)
                throw new InvalidOperationException($"Cost factor of '{rung.Id}' must be positive with low <= high.");
            low *= factorLow;
            high *= factorHigh;
            derivation.Add(string.Create(CultureInfo.InvariantCulture, $"x {factorLow:0.####} to {factorHigh:0.####}: {factor.Note}"));
        }
        var credits = cli == DialCli.Codex ? CreditFactor(rung.ModelId) : null;
        if (credits is not null)
            derivation.Add(string.Create(CultureInfo.InvariantCulture, $"Codex credits = list USD x {credits:0.##} (Standard credit rate card) [V]"));
        else if (cli == DialCli.Claude && !claudeMeasured)
            derivation.Add("Claude weekly-limit weight: list price used as a flagged proxy; no vendor publishes a per-model weight.");
        return new DialRungCost
        {
            Status = DialCostStatus.Estimated,
            Unit = unit,
            ListUsdLow = decimal.Round(low, 4),
            ListUsdHigh = decimal.Round(high, 4),
            CodexCreditsLow = credits is null ? null : decimal.Round(low * credits.Value, 2),
            CodexCreditsHigh = credits is null ? null : decimal.Round(high * credits.Value, 2),
            QuotaBasis = QuotaBasisFor(cli, credits, claudeMeasured),
            MeasuredQuotaPointsPerAcceptedCard = rung.MeasuredQuotaWeight?.PercentagePointsPerAcceptedCard,
            Derivation = derivation,
        };
    }

    private static DialQuotaBasis QuotaBasisFor(DialCli cli, decimal? credits, bool claudeMeasured) => cli switch
    {
        DialCli.Codex when credits is not null => DialQuotaBasis.CodexCreditRate,
        DialCli.Codex => DialQuotaBasis.CodexCreditsUnavailable,
        _ when claudeMeasured => DialQuotaBasis.ClaudeMeasuredWeight,
        _ => DialQuotaBasis.ClaudeListPriceProxy,
    };

    private (decimal Usd, string ModelId, DialCostUnit Unit, string Text) ResolveAnchor(DialRung rung)
    {
        var anchor = rung.ExpectedCost!.Anchor;
        if (anchor.FleetRowId is { } rowId)
        {
            var row = FindFleetRow(rowId) ?? throw new InvalidOperationException($"Rung '{rung.Id}' anchors on unknown fleet row '{rowId}'.");
            return (row.MedianListUsdPerAcceptedCard, row.ModelId, DialCostUnit.AcceptedCard,
                string.Create(CultureInfo.InvariantCulture, $"Anchor {row.Id}: {row.MedianListUsdPerAcceptedCard:0.00} USD median list price per accepted card ({row.Accepted} of {row.Cards} accepted) [F]"));
        }
        var resultId = anchor.BenchmarkResultId ?? throw new InvalidOperationException($"Rung '{rung.Id}' has no cost anchor.");
        var result = FindBenchmarkResult(resultId) ?? throw new InvalidOperationException($"Rung '{rung.Id}' anchors on unknown benchmark result '{resultId}'.");
        var cost = result.SecondaryMetrics?.CostPerTaskUsd ?? throw new InvalidOperationException($"Anchor '{resultId}' publishes no cost per task.");
        return (cost, result.ModelId.Value, DialCostUnit.BenchmarkTask,
            string.Create(CultureInfo.InvariantCulture, $"Anchor {result.Id}: {cost:0.00} USD per benchmark task [{result.EvidenceClass}{(result.CostDerivation is null ? "" : ", cost derived")}]"));
    }

    private decimal Term(DialRatioRef term, DialRung rung, string anchorModel)
    {
        var kinds = (term.BenchmarkRatio is null ? 0 : 1) + (term.FleetRatio is null ? 0 : 1) + (term.Reprice is null ? 0 : 1) + (term.Constant is null ? 0 : 1);
        if (kinds != 1)
            throw new InvalidOperationException($"A cost term of '{rung.Id}' must name exactly one kind.");
        if (term.Constant is { } constant)
        {
            if (string.IsNullOrWhiteSpace(term.Reason))
                throw new InvalidOperationException($"A constant cost term of '{rung.Id}' needs a reason.");
            return constant;
        }
        if (term.BenchmarkRatio is { Count: 2 } pair)
        {
            var numerator = FindBenchmarkResult(pair[0])?.SecondaryMetrics?.CostPerTaskUsd;
            var denominator = FindBenchmarkResult(pair[1])?.SecondaryMetrics?.CostPerTaskUsd;
            if (numerator is null || denominator is null or 0)
                throw new InvalidOperationException($"Benchmark ratio of '{rung.Id}' needs two rows with a cost per task.");
            return numerator.Value / denominator.Value;
        }
        if (term.FleetRatio is { Count: 2 } rows)
        {
            var numerator = FindFleetRow(rows[0]) ?? throw new InvalidOperationException($"Unknown fleet row '{rows[0]}' in '{rung.Id}'.");
            var denominator = FindFleetRow(rows[1]) ?? throw new InvalidOperationException($"Unknown fleet row '{rows[1]}' in '{rung.Id}'.");
            return numerator.MedianListUsdPerAcceptedCard / denominator.MedianListUsdPerAcceptedCard;
        }
        if (term.Reprice is { } reprice)
            return Reprice(reprice, rung, anchorModel);
        throw new InvalidOperationException($"Malformed cost term in '{rung.Id}'.");
    }

    /// <summary>
    /// Same token profile at another model's prices. With cache-read share s of the anchor's list-price cost:
    /// factor = s x (cache-read price ratio) + (1 - s) x (input price ratio). Valid only when output and
    /// cache-write prices keep the same multiple of input price on both models, which is checked.
    /// </summary>
    private decimal Reprice(DialRepriceRef reprice, DialRung rung, string anchorModel)
    {
        if (reprice.FromModelId != anchorModel || reprice.ToModelId != rung.ModelId)
            throw new InvalidOperationException($"Reprice in '{rung.Id}' must go from the anchor model to the rung model.");
        decimal share;
        if (reprice.CacheReadObservationId is { } observationId)
        {
            if (reprice.CacheReadShare is not null)
                throw new InvalidOperationException($"Reprice in '{rung.Id}' names both a measured and an assumed share.");
            var observation = _cacheObservations.GetValueOrDefault(observationId)
                ?? throw new InvalidOperationException($"Unknown cache-read observation '{observationId}' in '{rung.Id}'.");
            share = observation.CacheReadShareOfListCost
                ?? throw new InvalidOperationException($"Cache-read observation '{observationId}' has no cost share.");
            if (observation.ModelId != anchorModel)
                throw new InvalidOperationException($"Cache-read observation '{observationId}' belongs to another model than the anchor of '{rung.Id}'.");
        }
        else
        {
            share = reprice.CacheReadShare ?? throw new InvalidOperationException($"Reprice in '{rung.Id}' needs a cache-read share.");
        }
        var at = AsOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var from = _prices.ResolvePrice(reprice.FromModelId, at).Price ?? throw new InvalidOperationException($"No price for '{reprice.FromModelId}'.");
        var to = _prices.ResolvePrice(reprice.ToModelId, at).Price ?? throw new InvalidOperationException($"No price for '{reprice.ToModelId}'.");
        if (from.CacheReadPerMTok is not { } fromCache || to.CacheReadPerMTok is not { } toCache)
            throw new InvalidOperationException($"Reprice in '{rung.Id}' needs cache-read prices on both models.");
        if (from.OutputPerMTok / from.InputPerMTok != to.OutputPerMTok / to.InputPerMTok
            || (from.CacheWritePerMTok is { } fromWrite && to.CacheWritePerMTok is { } toWrite
                && fromWrite / from.InputPerMTok != toWrite / to.InputPerMTok))
            throw new InvalidOperationException($"Reprice in '{rung.Id}' would need a split of the non-cache cost; output or write prices are not proportional to input.");
        return share * (toCache / fromCache) + (1 - share) * (to.InputPerMTok / from.InputPerMTok);
    }

    /// <summary>Credits per list-price USD from the latest Codex credit rate card, when uniform across components.</summary>
    private decimal? CreditFactor(string modelId)
    {
        var at = AsOfDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var card = _subscriptions.Observations
            .Where(o => o.Kind == "credit-rate-card" && o.ModelId == modelId && o.ObservedFromUtc <= at)
            .MaxBy(o => o.ObservedToUtc);
        var price = _prices.ResolvePrice(modelId, at).Price;
        if (card is null || price is null || card.InputCreditsPerMTok is not { } input || card.OutputCreditsPerMTok is not { } output
            || card.CacheReadCreditsPerMTok is not { } cache || price.CacheReadPerMTok is not { } cacheList)
            return null;
        var factor = input / price.InputPerMTok;
        return output / price.OutputPerMTok == factor && cache / cacheList == factor ? factor : null;
    }
}

#pragma warning restore CS1591
