using System.Diagnostics;
using System.Text.Json;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Runs definitions in fresh workspaces and persists immutable raw results plus derived reports.</summary>
public sealed class BenchmarkRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IBenchmarkInvoker _invoker;
    public BenchmarkRunner(IBenchmarkInvoker invoker) => _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    public event Action<BenchmarkRunEvent>? EventOccurred;

    public async Task<(BenchmarkRunResult Result, BenchmarkComparisonReport Report)> RunAsync(
        BenchmarkDefinition definition, string repositoryRoot, string? runId = null, CancellationToken cancellationToken = default)
    {
        Validate(definition);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var started = DateTime.UtcNow;
        runId ??= started.ToString("yyyyMMddTHHmmssfffZ");
        var resultsDirectory = Path.Combine(repositoryRoot, "benchmarks", "results", definition.Id);
        Directory.CreateDirectory(resultsDirectory);
        var resultPath = Path.Combine(resultsDirectory, runId + ".json");
        var reportPath = Path.Combine(resultsDirectory, runId + ".report.json");
        if (File.Exists(resultPath) || File.Exists(reportPath)) throw new IOException($"Benchmark run '{runId}' already exists; results are append-only.");

        var seed = ResolveWithin(repositoryRoot, definition.Task.SeedWorkspace);
        if (!Directory.Exists(seed)) throw new DirectoryNotFoundException($"Seed workspace does not exist: {seed}");
        var scratchRoot = Path.Combine(Path.GetTempPath(), "token-economy-benchmarks", definition.Id, runId);
        var cases = new List<BenchmarkCaseResult>();
        EventOccurred?.Invoke(new("benchmark.run.started", new Dictionary<string, object?> { ["setupId"] = definition.Id, ["runId"] = runId }));
        try
        {
            foreach (var variant in definition.Variants)
            for (var repetition = 1; repetition <= definition.Repetitions; repetition++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workspace = Path.Combine(scratchRoot, variant.Id, repetition.ToString());
                CopyDirectory(seed, workspace);
                var timer = Stopwatch.StartNew();
                BenchmarkInvocationResponse response;
                using (var invocationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    invocationTimeout.CancelAfter(TimeSpan.FromSeconds(definition.InvocationTimeoutSeconds));
                    try { response = await _invoker.InvokeAsync(new(definition.Id, variant, definition.Task.Prompt, workspace, repetition, definition.Task.ResponseFile), invocationTimeout.Token); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        response = new() { ExitCode = -1, Usage = default, Error = $"Invocation timed out after {definition.InvocationTimeoutSeconds} seconds." };
                    }
                }
                var totalTokens = response.Usage.Input + response.Usage.Output + response.Usage.CacheRead + response.Usage.CacheWrite;
                string? failure = response.ExitCode == 0 ? null : response.Error ?? $"Invoker exited {response.ExitCode}.";
                int? evaluationExit = null;
                if (failure is null && definition.CostCaps?.MaxTotalTokensPerInvocation is { } tokenCap && totalTokens > tokenCap)
                    failure = $"Token cap exceeded ({totalTokens} > {tokenCap}).";
                if (failure is null && definition.CostCaps?.MaxUsdPerInvocation is { } usdCap && response.CostUsd is { } reportedCost && reportedCost > usdCap)
                    failure = $"Cost cap exceeded ({reportedCost} > {usdCap} USD).";
                if (failure is null)
                {
                    evaluationExit = await EvaluateAsync(definition.SuccessCriteria, workspace, repositoryRoot, cancellationToken);
                    if (evaluationExit != definition.SuccessCriteria.ExpectedExitCode)
                        failure = $"Success command exited {evaluationExit}; expected {definition.SuccessCriteria.ExpectedExitCode}.";
                }
                var metrics = ReadMetrics(definition.SuccessCriteria.MetricsFile, workspace);
                var outcomeScore = definition.SuccessCriteria.PrimaryMetric is { } primaryMetric
                    && metrics.TryGetValue(primaryMetric, out var primaryValue)
                        ? primaryValue
                        : (decimal?)null;
                var cost = response.CostUsd ?? ResolveCatalogCost(response.Usage, variant.Model, started);
                timer.Stop();
                cases.Add(new()
                {
                    VariantId = variant.Id, Model = variant.Model, ThinkingLevel = variant.ThinkingLevel,
                    Repetition = repetition, Succeeded = failure is null, InvocationExitCode = response.ExitCode,
                    EvaluationExitCode = evaluationExit, Usage = response.Usage, CostUsd = cost,
                    DurationMs = timer.ElapsedMilliseconds, FailureReason = failure, Metrics = metrics,
                    OutcomeScore = outcomeScore,
                });
                EventOccurred?.Invoke(new("benchmark.case.completed", new Dictionary<string, object?>
                { ["setupId"] = definition.Id, ["runId"] = runId, ["variantId"] = variant.Id, ["repetition"] = repetition,
                  ["succeeded"] = failure is null, ["durationMs"] = timer.ElapsedMilliseconds, ["totalTokens"] = totalTokens,
                  ["failureReason"] = failure }));
            }

            var result = new BenchmarkRunResult
            {
                SchemaVersion = 1, SetupId = definition.Id, RunId = runId,
                StartedAtUtc = started, CompletedAtUtc = DateTime.UtcNow,
                TaskClass = definition.Task.TaskClass, Capability = definition.Task.Capability,
                PrimaryMetric = definition.SuccessCriteria.PrimaryMetric,
                HigherPrimaryMetricIsBetter = definition.SuccessCriteria.HigherPrimaryMetricIsBetter,
                Cases = cases,
            };
            var report = Compare(result);
            await WriteNewAsync(resultPath, result, cancellationToken);
            await WriteNewAsync(reportPath, report, cancellationToken);
            EventOccurred?.Invoke(new("benchmark.run.completed", new Dictionary<string, object?>
            { ["setupId"] = definition.Id, ["runId"] = runId, ["cases"] = cases.Count, ["winner"] = report.Winner }));
            return (result, report);
        }
        finally
        {
            if (Directory.Exists(scratchRoot))
            try { Directory.Delete(scratchRoot, recursive: true); }
            catch (IOException error)
            {
                EventOccurred?.Invoke(new("benchmark.workspace.cleanup_failed", new Dictionary<string, object?>
                { ["setupId"] = definition.Id, ["runId"] = runId, ["workspace"] = scratchRoot, ["error"] = error.Message }));
            }
        }
    }

    public static BenchmarkComparisonReport Compare(BenchmarkRunResult result)
    {
        var variants = result.Cases.GroupBy(c => c.VariantId).Select(group =>
        {
            var tokens = group.Sum(c => c.Usage.Input + c.Usage.Output + c.Usage.CacheRead + c.Usage.CacheWrite);
            var costsAvailable = group.All(c => c.CostUsd is not null);
            var totalCost = costsAvailable ? group.Sum(c => c.CostUsd!.Value) : (decimal?)null;
            var successful = group.Count(c => c.Succeeded);
            var metricNames = group.SelectMany(c => c.Metrics.Keys).Distinct(StringComparer.Ordinal);
            var averageMetrics = metricNames.ToDictionary(
                name => name,
                name => group.Where(c => c.Metrics.ContainsKey(name)).Average(c => c.Metrics[name]),
                StringComparer.Ordinal);
            return new BenchmarkVariantComparison
            {
                VariantId = group.Key, Runs = group.Count(), Successes = successful,
                SuccessRate = (decimal)successful / group.Count(), TotalTokens = tokens,
                AverageTokens = (decimal)tokens / group.Count(),
                TotalCostUsd = totalCost,
                CostPerSuccessfulOutcomeUsd = totalCost is not null && successful > 0 ? totalCost / successful : null,
                AverageDurationMs = (decimal)group.Sum(c => c.DurationMs) / group.Count(),
                AverageOutcomeScore = group.Any(c => c.OutcomeScore is not null)
                    ? group.Where(c => c.OutcomeScore is not null).Average(c => c.OutcomeScore!.Value)
                    : null,
                AverageMetrics = averageMetrics,
            };
        }).OrderByDescending(v => v.SuccessRate)
            .ThenBy(v => v, new OutcomeScoreComparer(result.HigherPrimaryMetricIsBetter))
            .ThenBy(v => v.AverageTokens).ThenBy(v => v.AverageDurationMs)
            .ThenBy(v => v.VariantId, StringComparer.Ordinal).ToList();
        var winner = variants.Count == 0 || variants[0].Successes == 0 ? null : variants[0].VariantId;
        decimal? qualityDelta = variants.Count < 2 ? null : variants[0].SuccessRate - variants[1].SuccessRate;
        decimal? costDelta = variants.Count < 2 || variants[0].TotalCostUsd is null || variants[1].TotalCostUsd is null
            ? null : variants[0].TotalCostUsd - variants[1].TotalCostUsd;
        return new BenchmarkComparisonReport
        {
            SetupId = result.SetupId, RunId = result.RunId, Winner = winner,
            WinnerReason = variants.Count == 0
                ? "No cases."
                : winner is null
                    ? result.PrimaryMetric is null
                        ? "No successful cases; variants are ordered by tokens, duration, then variant id."
                        : $"No successful cases; variants are ordered by average {result.PrimaryMetric}, tokens, duration, then variant id."
                    : result.PrimaryMetric is null
                        ? "Highest success rate; ties break on average tokens, duration, then variant id."
                        : $"Highest success rate; ties break on average {result.PrimaryMetric}, tokens, duration, then variant id.",
            Variants = variants, CostDeltaUsd = costDelta, QualityDelta = qualityDelta,
            PrimaryMetric = result.PrimaryMetric,
        };
    }

    public static BenchmarkDefinition LoadDefinition(string path) =>
        JsonSerializer.Deserialize<BenchmarkDefinition>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"Could not deserialize benchmark setup: {path}");

    private static void Validate(BenchmarkDefinition value)
    {
        if (value.SchemaVersion != 1) throw new InvalidDataException($"Unsupported benchmark schema version {value.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(value.Id) || value.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("Setup id must be a valid file name.");
        if (value.FormatAttribution.Sources.Count < 1) throw new InvalidDataException("Format attribution requires at least one source suite.");
        if (value.FormatAttribution.Sources.Any(source =>
                string.IsNullOrWhiteSpace(source.Suite) ||
                !Uri.TryCreate(source.Reference, UriKind.Absolute, out _) ||
                source.Borrowed.Count < 1 ||
                source.Borrowed.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("Each format source requires a suite, absolute reference, and at least one borrowed element.");
        if (value.FormatAttribution.Deviations.Any(deviation =>
                string.IsNullOrWhiteSpace(deviation.Difference) || string.IsNullOrWhiteSpace(deviation.Reason)))
            throw new InvalidDataException("Each format deviation requires both the difference and its reason.");
        if (value.Variants.Count < 2) throw new InvalidDataException("A benchmark requires at least two variants.");
        if (value.Repetitions < 1) throw new InvalidDataException("Repetitions must be at least one.");
        if (value.InvocationTimeoutSeconds < 1) throw new InvalidDataException("Invocation timeout must be at least one second.");
        if (value.Variants.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != value.Variants.Count) throw new InvalidDataException("Variant ids must be unique.");
        if (value.SuccessCriteria.PrimaryMetric is not null && value.SuccessCriteria.MetricsFile is null)
            throw new InvalidDataException("A primary metric requires successCriteria.metricsFile.");
    }

    private static string ResolveWithin(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("Seed workspace must be repository-relative.");
        var resolved = Path.GetFullPath(Path.Combine(root, relative));
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Seed workspace escapes repository root.");
        return resolved;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static async Task<int> EvaluateAsync(
        BenchmarkSuccessCriteria criteria, string workspace, string repositoryRoot, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(criteria.TimeoutSeconds));
        using var process = new Process { StartInfo = new(criteria.Command) { WorkingDirectory = workspace, UseShellExecute = false } };
        foreach (var argument in criteria.Arguments)
            process.StartInfo.ArgumentList.Add(argument
                .Replace("{workspace}", workspace, StringComparison.Ordinal)
                .Replace("{repositoryRoot}", repositoryRoot, StringComparison.Ordinal));
        process.Start();
        try { await process.WaitForExitAsync(timeout.Token); return process.ExitCode; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { process.Kill(entireProcessTree: true); return -1; }
    }

    private static IReadOnlyDictionary<string, decimal> ReadMetrics(string? relativePath, string workspace)
    {
        if (relativePath is null) return new Dictionary<string, decimal>();
        var path = ResolveWithin(workspace, relativePath);
        if (!File.Exists(path)) return new Dictionary<string, decimal>();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Benchmark metrics must be a JSON object: {relativePath}");
        var metrics = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDecimal(out var value))
                throw new InvalidDataException($"Benchmark metric '{property.Name}' must be numeric.");
            metrics.Add(property.Name, value);
        }
        return metrics;
    }

    private static decimal? ResolveCatalogCost(TokenUsage usage, string model, DateTime atUtc)
    {
        var totalTokens = usage.Input + usage.Output + usage.CacheRead + usage.CacheWrite;
        if (totalTokens <= 0) return null;
        var cost = ModelPriceCatalog.Default.ComputeCost(model, usage, atUtc);
        return cost.HasPrice ? cost.Total : null;
    }

    private static async Task WriteNewAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, value, Json, cancellationToken);
    }

    private sealed class OutcomeScoreComparer(bool higherIsBetter) : IComparer<BenchmarkVariantComparison>
    {
        public int Compare(BenchmarkVariantComparison? left, BenchmarkVariantComparison? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;
            if (left.AverageOutcomeScore is null) return right.AverageOutcomeScore is null ? 0 : 1;
            if (right.AverageOutcomeScore is null) return -1;
            return higherIsBetter
                ? right.AverageOutcomeScore.Value.CompareTo(left.AverageOutcomeScore.Value)
                : left.AverageOutcomeScore.Value.CompareTo(right.AverageOutcomeScore.Value);
        }
    }
}

public sealed record BenchmarkRunEvent(string Name, IReadOnlyDictionary<string, object?> Context);
