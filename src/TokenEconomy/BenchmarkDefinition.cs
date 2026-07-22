using System.Text.Json.Serialization;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>A versioned, transport-independent definition for a controlled model comparison.</summary>
public sealed record BenchmarkDefinition
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required BenchmarkTask Task { get; init; }
    public required IReadOnlyList<BenchmarkVariant> Variants { get; init; }
    public int Repetitions { get; init; } = 1;
    public int InvocationTimeoutSeconds { get; init; } = 300;
    public required BenchmarkSuccessCriteria SuccessCriteria { get; init; }
    public BenchmarkCostCaps? CostCaps { get; init; }
}

public sealed record BenchmarkTask
{
    public required string Prompt { get; init; }
    public required string SeedWorkspace { get; init; }
    public string? ResponseFile { get; init; }
}

public sealed record BenchmarkVariant
{
    public required string Id { get; init; }
    public required string Model { get; init; }
    public string? ThinkingLevel { get; init; }
}

public sealed record BenchmarkSuccessCriteria
{
    public required string Command { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public int ExpectedExitCode { get; init; }
    public int TimeoutSeconds { get; init; } = 120;
}

public sealed record BenchmarkCostCaps
{
    public long? MaxTotalTokensPerInvocation { get; init; }
    public decimal? MaxUsdPerInvocation { get; init; }
}

public sealed record BenchmarkInvocationRequest(
    string SetupId, BenchmarkVariant Variant, string Prompt, string Workspace, int Repetition, string? ResponseFile);

public sealed record BenchmarkInvocationResponse
{
    public required int ExitCode { get; init; }
    public required TokenUsage Usage { get; init; }
    public decimal? CostUsd { get; init; }
    public string? FinalResponse { get; init; }
    public string? Error { get; init; }
}

public interface IBenchmarkInvoker
{
    Task<BenchmarkInvocationResponse> InvokeAsync(BenchmarkInvocationRequest request, CancellationToken cancellationToken = default);
}

public sealed record BenchmarkCaseResult
{
    public required string VariantId { get; init; }
    public required string Model { get; init; }
    public string? ThinkingLevel { get; init; }
    public required int Repetition { get; init; }
    public required bool Succeeded { get; init; }
    public required int InvocationExitCode { get; init; }
    public int? EvaluationExitCode { get; init; }
    public required TokenUsage Usage { get; init; }
    public decimal? CostUsd { get; init; }
    public required long DurationMs { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record BenchmarkRunResult
{
    public required int SchemaVersion { get; init; }
    public required string SetupId { get; init; }
    public required string RunId { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required IReadOnlyList<BenchmarkCaseResult> Cases { get; init; }
}

public sealed record BenchmarkVariantComparison
{
    public required string VariantId { get; init; }
    public required int Runs { get; init; }
    public required int Successes { get; init; }
    public required decimal SuccessRate { get; init; }
    public required long TotalTokens { get; init; }
    public required decimal AverageTokens { get; init; }
    public decimal? TotalCostUsd { get; init; }
    public required decimal AverageDurationMs { get; init; }
}

public sealed record BenchmarkComparisonReport
{
    public required string SetupId { get; init; }
    public required string RunId { get; init; }
    public string? Winner { get; init; }
    public required string WinnerReason { get; init; }
    public required IReadOnlyList<BenchmarkVariantComparison> Variants { get; init; }
    public decimal? CostDeltaUsd { get; init; }
    public decimal? QualityDelta { get; init; }
}
