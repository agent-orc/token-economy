namespace TokenEconomy;

#pragma warning disable CS1591 // Matrix data records are described on the API entry points.

public sealed record BenchmarkWeight
{
    public required string BenchmarkTypeId { get; init; }
    public decimal Weight { get; init; } = 1m;
}

public readonly record struct BenchmarkTokenAssumption(
    long InputTokensPerTask,
    long OutputTokensPerTask,
    long CacheReadTokensPerTask = 0,
    long CacheWriteTokensPerTask = 0)
{
    internal TokenUsage ToUsage() => new(
        InputTokensPerTask, OutputTokensPerTask, CacheReadTokensPerTask, CacheWriteTokensPerTask);

    internal long Total => InputTokensPerTask + OutputTokensPerTask + CacheReadTokensPerTask + CacheWriteTokensPerTask;
}

public readonly record struct BenchmarkCellKey(ModelId ModelId, EffortLevel Effort);

public sealed record BenchmarkTokenPrices
{
    public required PriceStatus Status { get; init; }
    public string? Currency { get; init; }
    public decimal? InputPerMTok { get; init; }
    public decimal? OutputPerMTok { get; init; }
    public decimal? CacheReadPerMTok { get; init; }
    public decimal? CacheWritePerMTok { get; init; }
    public DateTime? ValidFromUtc { get; init; }
    public bool Unconfirmed { get; init; }
}

public enum BenchmarkCostBasis
{
    PublishedPerTask,
    DeclaredTokenAssumption,
    Unavailable,
}

public sealed record ModelBenchmarkCell
{
    public required BenchmarkCellKey Key { get; init; }
    public required decimal Score { get; init; }
    public required bool ScoreIsNormalized { get; init; }
    public required IReadOnlyDictionary<string, decimal> ScoresByBenchmark { get; init; }
    public required BenchmarkTokenPrices Prices { get; init; }
    public long? PublishedInputTokensPerTask { get; init; }
    public long? PublishedOutputTokensPerTask { get; init; }
    public decimal? PublishedCostPerTaskUsd { get; init; }
    public decimal? CostPerTaskUsd { get; init; }
    public required BenchmarkCostBasis CostBasis { get; init; }
    public decimal? BlendedPricePerMillionTokensUsd { get; init; }
    public decimal? ScorePerDollar { get; init; }
    public decimal? ScoreDeltaToReference { get; init; }
    public decimal? CostDeltaToReferenceUsd { get; init; }
    public required int EvidenceAgeDays { get; init; }
    public required bool IsStale { get; init; }
    public required IReadOnlyList<BenchmarkResult> Evidence { get; init; }
}

public sealed record ModelBenchmarkMatrixResult
{
    public required IReadOnlyList<BenchmarkWeight> Benchmarks { get; init; }
    public required BenchmarkTokenAssumption TokenAssumption { get; init; }
    public BenchmarkCellKey? Reference { get; init; }
    public required DateTime AsOfUtc { get; init; }
    public required IReadOnlyList<ModelBenchmarkCell> Cells { get; init; }
}

public sealed record BenchmarkCandidate
{
    public required ModelBenchmarkCell Cell { get; init; }
    public required IReadOnlyList<AgedBenchmarkEvidence> Evidence { get; init; }
}

public sealed record AgedBenchmarkEvidence
{
    public required BenchmarkResult Result { get; init; }
    public required int AgeDays { get; init; }
    public required bool IsStale { get; init; }
}

/// <summary>
/// Pure price-performance projection over append-only benchmark evidence and <see cref="ModelPriceCatalog"/>.
/// It recommends comparable candidates; it never selects or routes a workload.
/// </summary>
public sealed class ModelBenchmarkMatrix
{
    public const int StaleAfterDays = 90;
    private readonly BenchmarkEvidenceCatalog _evidence;
    private readonly ModelPriceCatalog _prices;

    public ModelBenchmarkMatrix(BenchmarkEvidenceCatalog evidence, ModelPriceCatalog prices)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(prices);
        _evidence = evidence;
        _prices = prices;
    }

    public static ModelBenchmarkMatrix Default { get; } = new(BenchmarkEvidenceCatalog.Default, ModelPriceCatalog.Default);

    public ModelBenchmarkMatrixResult Build(
        string benchmarkTypeId,
        BenchmarkTokenAssumption tokenAssumption,
        BenchmarkCellKey? reference,
        DateTime asOfUtc) =>
        Build([new BenchmarkWeight { BenchmarkTypeId = benchmarkTypeId }], tokenAssumption, reference, asOfUtc);

    public ModelBenchmarkMatrixResult Build(
        IEnumerable<BenchmarkWeight> benchmarks,
        BenchmarkTokenAssumption tokenAssumption,
        BenchmarkCellKey? reference,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(benchmarks);
        var weights = benchmarks.ToArray();
        Validate(weights, tokenAssumption, asOfUtc);
        var typeById = weights.ToDictionary(
            weight => weight.BenchmarkTypeId,
            weight => _evidence.FindType(weight.BenchmarkTypeId)!,
            StringComparer.Ordinal);
        var selectedIds = weights.Select(weight => weight.BenchmarkTypeId).ToHashSet(StringComparer.Ordinal);
        var grouped = _evidence.Results
            .Where(result => selectedIds.Contains(result.BenchmarkTypeId) && result.PublishedAt <= DateOnly.FromDateTime(asOfUtc))
            .GroupBy(result => new BenchmarkCellKey(result.ModelId, result.ReasoningEffort))
            .ToArray();
        var cells = new List<ModelBenchmarkCell>();

        foreach (var group in grouped)
        {
            var chosen = group
                .GroupBy(result => result.BenchmarkTypeId, StringComparer.Ordinal)
                .ToDictionary(
                    bucket => bucket.Key,
                    bucket => bucket.OrderByDescending(result => result.PublishedAt)
                        .ThenByDescending(result => result.RetrievedAt)
                        .ThenBy(result => result.SourceUrl, StringComparer.Ordinal)
                        .First(),
                    StringComparer.Ordinal);
            if (weights.Any(weight => !chosen.ContainsKey(weight.BenchmarkTypeId)))
                continue;

            var score = weights.Length == 1
                ? chosen[weights[0].BenchmarkTypeId].Score
                : weights.Sum(weight => Normalize(chosen[weight.BenchmarkTypeId].Score, typeById[weight.BenchmarkTypeId]) * weight.Weight)
                    / weights.Sum(weight => weight.Weight);
            var single = weights.Length == 1 ? chosen[weights[0].BenchmarkTypeId] : null;
            var publishedCosts = chosen.Values.Select(result => result.SecondaryMetrics?.CostPerTaskUsd).ToArray();
            decimal? publishedCost = single?.SecondaryMetrics?.CostPerTaskUsd;
            if (weights.Length > 1 && publishedCosts.All(cost => cost is not null))
                publishedCost = weights.Sum(weight => chosen[weight.BenchmarkTypeId].SecondaryMetrics!.CostPerTaskUsd!.Value * weight.Weight)
                    / weights.Sum(weight => weight.Weight);

            var price = _prices.ResolvePrice(group.Key.ModelId, asOfUtc);
            var assumed = _prices.ComputeCost(group.Key.ModelId, tokenAssumption.ToUsage(), asOfUtc);
            var cost = publishedCost ?? assumed.Total;
            var costBasis = publishedCost is not null
                ? BenchmarkCostBasis.PublishedPerTask
                : assumed.Total is not null ? BenchmarkCostBasis.DeclaredTokenAssumption : BenchmarkCostBasis.Unavailable;
            var ages = chosen.Values.Select(result => Math.Max(0, DateOnly.FromDateTime(asOfUtc).DayNumber - result.PublishedAt.DayNumber)).ToArray();
            cells.Add(new ModelBenchmarkCell
            {
                Key = group.Key,
                Score = score,
                ScoreIsNormalized = weights.Length > 1,
                ScoresByBenchmark = chosen.ToDictionary(pair => pair.Key, pair => pair.Value.Score, StringComparer.Ordinal),
                Prices = Project(price),
                PublishedInputTokensPerTask = single?.SecondaryMetrics?.InputTokensPerTask,
                PublishedOutputTokensPerTask = single?.SecondaryMetrics?.OutputTokensPerTask,
                PublishedCostPerTaskUsd = publishedCost,
                CostPerTaskUsd = cost,
                CostBasis = costBasis,
                BlendedPricePerMillionTokensUsd = assumed.Total is null
                    ? null : assumed.Total.Value / tokenAssumption.Total * 1_000_000m,
                ScorePerDollar = cost is > 0 ? score / cost.Value : null,
                EvidenceAgeDays = ages.Max(),
                IsStale = ages.Any(age => age > StaleAfterDays),
                Evidence = group.OrderByDescending(result => result.PublishedAt)
                    .ThenByDescending(result => result.RetrievedAt)
                    .ThenBy(result => result.Id, StringComparer.Ordinal)
                    .ToArray(),
            });
        }

        cells.Sort((left, right) =>
        {
            var model = StringComparer.Ordinal.Compare(left.Key.ModelId.Value, right.Key.ModelId.Value);
            return model != 0 ? model : EffortRank(left.Key.Effort).CompareTo(EffortRank(right.Key.Effort));
        });
        if (reference is { } referenceKey)
        {
            var referenceCell = cells.SingleOrDefault(cell => cell.Key == referenceKey);
            if (referenceCell is not null)
            {
                for (var index = 0; index < cells.Count; index++)
                    cells[index] = cells[index] with
                    {
                        ScoreDeltaToReference = cells[index].Score - referenceCell.Score,
                        CostDeltaToReferenceUsd = cells[index].CostPerTaskUsd is { } costValue
                            && referenceCell.CostPerTaskUsd is { } referenceCost
                            ? costValue - referenceCost : null,
                    };
            }
        }

        return new()
        {
            Benchmarks = weights,
            TokenAssumption = tokenAssumption,
            Reference = reference,
            AsOfUtc = asOfUtc,
            Cells = cells,
        };
    }

    public IReadOnlyList<BenchmarkCandidate> FindCandidates(
        BenchmarkCellKey current,
        string benchmarkTypeId,
        BenchmarkTokenAssumption tokenAssumption,
        DateTime asOfUtc)
    {
        var matrix = Build(benchmarkTypeId, tokenAssumption, current, asOfUtc);
        var baseline = matrix.Cells.SingleOrDefault(cell => cell.Key == current)
            ?? throw new ArgumentException("The current model/effort cell has no evidence for this benchmark.", nameof(current));
        return matrix.Cells
            .Where(cell => cell.Key != current && cell.Score >= baseline.Score && Cheaper(cell, baseline))
            .OrderByDescending(cell => cell.Score)
            .ThenBy(cell => cell.CostPerTaskUsd ?? decimal.MaxValue)
            .ThenBy(cell => cell.BlendedPricePerMillionTokensUsd ?? decimal.MaxValue)
            .ThenBy(cell => cell.Key.ModelId.Value, StringComparer.Ordinal)
            .ThenBy(cell => EffortRank(cell.Key.Effort))
            .Select(cell => new BenchmarkCandidate
            {
                Cell = cell,
                Evidence = cell.Evidence.Select(result =>
                {
                    var age = Math.Max(0, DateOnly.FromDateTime(asOfUtc).DayNumber - result.PublishedAt.DayNumber);
                    return new AgedBenchmarkEvidence { Result = result, AgeDays = age, IsStale = age > StaleAfterDays };
                }).ToArray(),
            })
            .ToArray();
    }

    private static bool Cheaper(ModelBenchmarkCell candidate, ModelBenchmarkCell current) =>
        candidate.CostPerTaskUsd is { } candidateCost && current.CostPerTaskUsd is { } currentCost && candidateCost < currentCost
        || candidate.BlendedPricePerMillionTokensUsd is { } candidatePrice
            && current.BlendedPricePerMillionTokensUsd is { } currentPrice && candidatePrice < currentPrice;

    private void Validate(BenchmarkWeight[] weights, BenchmarkTokenAssumption assumption, DateTime asOfUtc)
    {
        if (weights.Length == 0)
            throw new ArgumentException("At least one benchmark type is required.", nameof(weights));
        if (weights.Any(weight => string.IsNullOrWhiteSpace(weight.BenchmarkTypeId) || weight.Weight <= 0))
            throw new ArgumentException("Benchmark ids and positive weights are required.", nameof(weights));
        if (weights.Select(weight => weight.BenchmarkTypeId).Distinct(StringComparer.Ordinal).Count() != weights.Length)
            throw new ArgumentException("A weighted benchmark set cannot contain duplicate ids.", nameof(weights));
        foreach (var weight in weights)
            if (_evidence.FindType(weight.BenchmarkTypeId) is null)
                throw new ArgumentException($"Unknown benchmark type '{weight.BenchmarkTypeId}'.", nameof(weights));
        if (assumption.InputTokensPerTask < 0 || assumption.OutputTokensPerTask < 0
            || assumption.CacheReadTokensPerTask < 0 || assumption.CacheWriteTokensPerTask < 0
            || assumption.Total <= 0)
            throw new ArgumentException("The declared token assumption must contain a positive, non-negative token mix.", nameof(assumption));
        if (asOfUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The matrix as-of timestamp must be UTC.", nameof(asOfUtc));
    }

    private static decimal Normalize(decimal score, BenchmarkType type)
    {
        var normalized = (score - type.MinimumScore) / (type.MaximumScore - type.MinimumScore) * 100m;
        return type.Direction == BenchmarkScoreDirection.HigherIsBetter ? normalized : 100m - normalized;
    }

    private static BenchmarkTokenPrices Project(PriceResolution resolution)
    {
        var price = resolution.Price;
        return new()
        {
            Status = resolution.Status,
            Currency = price?.Currency,
            InputPerMTok = price?.InputPerMTok,
            OutputPerMTok = price?.OutputPerMTok,
            CacheReadPerMTok = price?.CacheReadPerMTok,
            CacheWritePerMTok = price?.CacheWritePerMTok,
            ValidFromUtc = price?.ValidFrom,
            Unconfirmed = price?.Unconfirmed ?? false,
        };
    }

    private static int EffortRank(EffortLevel effort) => effort == EffortLevel.Unspecified ? int.MaxValue : (int)effort;
}

#pragma warning restore CS1591
