#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>The quality signal inferred from the final Agent Studio lane. It is intentionally a signal, not a claim of human evaluation.</summary>
public enum OutcomeQualitySignal { Unknown, Successful, NeedsReview, Unsuccessful }

/// <summary>A single, deduplicatable model run imported from an Agent Studio task card.</summary>
public sealed record AgentStudioRunRecord
{
    /// <summary>Stable card key from task storage.</summary>
    public required string TaskKey { get; init; }
    /// <summary>Attempt/run number within the task. Together with <see cref="TaskKey"/> this is the idempotency key.</summary>
    public required int Run { get; init; }
    public string? Project { get; init; }
    public string? Provider { get; init; }
    public required string Model { get; init; }
    public string? ThinkingLevel { get; init; }
    public string? CliType { get; init; }
    public string? TaskType { get; init; }
    /// <summary>Original pre-run card text retained for complexity calibration.</summary>
    public string? TaskPrompt { get; init; }
    public string? Area { get; init; }
    public string? EpicContext { get; init; }
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public IReadOnlyList<string> ReferencedFiles { get; init; } = [];
    public IReadOnlyList<string> ReferencedSubsystems { get; init; } = [];
    public int? DependencyFanOut { get; init; }
    public int? RepositoryFileCount { get; init; }
    public string? FinalLane { get; init; }
    public required TokenUsage Usage { get; init; }
    /// <summary>
    /// UTC instant used to resolve <see cref="CostEstimate"/> from the dated pricing catalog.
    /// This is normally the run completion timestamp; it remains distinct from
    /// <see cref="ObservedAtUtc"/> so a later card update cannot reprice a historical run.
    /// </summary>
    public required DateTime ExecutedAtUtc { get; init; }
    public decimal? CostEstimate { get; init; }
    public string? Currency { get; init; }
    public required PriceStatus CostStatus { get; init; }
    /// <summary>
    /// Consumer-facing qualification for <see cref="CostEstimate"/>. Catalog-derived
    /// estimates carry <see cref="ModelPrice.EstimatedListPricesCaveat"/> so a UI can
    /// render the list-price disclaimer alongside the number.
    /// </summary>
    public string? CostCaveat { get; init; }
    /// <summary>True when <see cref="CostEstimate"/> is based on published list prices rather than an invoice.</summary>
    public bool IsEstimatedListPrice => CostCaveat == ModelPrice.EstimatedListPricesCaveat;
    public required OutcomeQualitySignal Outcome { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public required DateTime ObservedAtUtc { get; init; }
}

/// <summary>Converts enriched imported runs into estimator calibration samples.</summary>
public static class ComplexityHistory
{
    /// <remarks>Cards without an imported prompt cannot be used for upfront similarity and are omitted.</remarks>
    public static IReadOnlyList<ComplexityHistorySample> FromRunRecords(IEnumerable<AgentStudioRunRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records
            .Where(record => !string.IsNullOrWhiteSpace(record.TaskPrompt))
            .Select(record => new ComplexityHistorySample
            {
                Card = new ComplexityCard
                {
                    TaskKey = record.TaskKey, Prompt = record.TaskPrompt!, Project = record.Project,
                    Area = record.Area, TaskType = record.TaskType, EpicContext = record.EpicContext,
                    AcceptanceCriteria = record.AcceptanceCriteria, ReferencedFiles = record.ReferencedFiles,
                    ReferencedSubsystems = record.ReferencedSubsystems, DependencyFanOut = record.DependencyFanOut,
                    RepositoryFileCount = record.RepositoryFileCount,
                },
                ActualTokens = record.Usage.Input + record.Usage.Output + record.Usage.CacheRead + record.Usage.CacheWrite,
                ActualDuration = record.StartedAtUtc is { } started && record.ExecutedAtUtc >= started
                    ? record.ExecutedAtUtc - started : TimeSpan.Zero,
                ReissueCount = Math.Max(0, record.Run - 1),
            }).ToArray();
    }
}

/// <summary>Writes imported records. Implementations must replace the record with the same task key and run rather than append it.</summary>
public interface IAgentStudioRunStore
{
    void Upsert(AgentStudioRunRecord record);
    IReadOnlyCollection<AgentStudioRunRecord> Records { get; }
}

/// <summary>Small in-memory store useful to hosts, tests, and command-line import jobs.</summary>
public sealed class InMemoryAgentStudioRunStore : IAgentStudioRunStore
{
    private readonly Dictionary<(string TaskKey, int Run), AgentStudioRunRecord> _records = new();
    public IReadOnlyCollection<AgentStudioRunRecord> Records => _records.Values;
    public void Upsert(AgentStudioRunRecord record) => _records[(record.TaskKey, record.Run)] = record;
}

/// <summary>A consumption/outcome view grouped by day, provider, model, and project.</summary>
public sealed record ModelRunView
{
    public required DateOnly Day { get; init; }
    public string? Provider { get; init; }
    public required string Model { get; init; }
    public string? Project { get; init; }
    public required int Runs { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
    public decimal? CostEstimate { get; init; }
    public required int SuccessfulRuns { get; init; }
    public required int NeedsReviewRuns { get; init; }
    public required int UnsuccessfulRuns { get; init; }
}

/// <summary>Builds model-over-time and per-project views from imported run records.</summary>
public static class ModelRunViews
{
    /// <summary>Consumption and outcome per model over time, optionally narrowed to a project.</summary>
    public static IReadOnlyList<ModelRunView> ByModelOverTime(IEnumerable<AgentStudioRunRecord> records, string? project = null)
        => Build(records.Where(r => project is null || string.Equals(r.Project, project, StringComparison.Ordinal)));

    /// <summary>Consumption and outcome per project (with model and day retained for drill-down).</summary>
    public static IReadOnlyList<ModelRunView> ByProject(IEnumerable<AgentStudioRunRecord> records)
        => Build(records);

    private static IReadOnlyList<ModelRunView> Build(IEnumerable<AgentStudioRunRecord> records) => records
        .GroupBy(r => (Day: DateOnly.FromDateTime(r.ObservedAtUtc), r.Provider, r.Model, r.Project))
        .OrderBy(g => g.Key.Day).ThenBy(g => g.Key.Project).ThenBy(g => g.Key.Model, StringComparer.Ordinal)
        .Select(g => new ModelRunView
        {
            Day = g.Key.Day, Provider = g.Key.Provider, Model = g.Key.Model, Project = g.Key.Project,
            Runs = g.Count(), InputTokens = g.Sum(r => r.Usage.Input), OutputTokens = g.Sum(r => r.Usage.Output),
            CacheReadTokens = g.Sum(r => r.Usage.CacheRead), CacheWriteTokens = g.Sum(r => r.Usage.CacheWrite),
            CostEstimate = g.Any(r => r.CostEstimate is null) ? null : g.Sum(r => r.CostEstimate!.Value),
            SuccessfulRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.Successful),
            NeedsReviewRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.NeedsReview),
            UnsuccessfulRuns = g.Count(r => r.Outcome == OutcomeQualitySignal.Unsuccessful),
        }).ToList();
}
