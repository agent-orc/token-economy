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
                if (failure is null && definition.CostCaps?.MaxUsdPerInvocation is { } usdCap && response.CostUsd is { } cost && cost > usdCap)
                    failure = $"Cost cap exceeded ({cost} > {usdCap} USD).";
                if (failure is null)
                {
                    evaluationExit = await EvaluateAsync(definition.SuccessCriteria, workspace, cancellationToken);
                    if (evaluationExit != definition.SuccessCriteria.ExpectedExitCode)
                        failure = $"Success command exited {evaluationExit}; expected {definition.SuccessCriteria.ExpectedExitCode}.";
                }
                timer.Stop();
                cases.Add(new()
                {
                    VariantId = variant.Id, Model = variant.Model, ThinkingLevel = variant.ThinkingLevel,
                    Repetition = repetition, Succeeded = failure is null, InvocationExitCode = response.ExitCode,
                    EvaluationExitCode = evaluationExit, Usage = response.Usage, CostUsd = response.CostUsd,
                    DurationMs = timer.ElapsedMilliseconds, FailureReason = failure,
                });
                EventOccurred?.Invoke(new("benchmark.case.completed", new Dictionary<string, object?>
                { ["setupId"] = definition.Id, ["runId"] = runId, ["variantId"] = variant.Id, ["repetition"] = repetition,
                  ["succeeded"] = failure is null, ["durationMs"] = timer.ElapsedMilliseconds, ["totalTokens"] = totalTokens,
                  ["failureReason"] = failure }));
            }

            var result = new BenchmarkRunResult { SchemaVersion = 1, SetupId = definition.Id, RunId = runId, StartedAtUtc = started, CompletedAtUtc = DateTime.UtcNow, Cases = cases };
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
            return new BenchmarkVariantComparison
            {
                VariantId = group.Key, Runs = group.Count(), Successes = group.Count(c => c.Succeeded),
                SuccessRate = (decimal)group.Count(c => c.Succeeded) / group.Count(), TotalTokens = tokens,
                AverageTokens = (decimal)tokens / group.Count(),
                TotalCostUsd = group.All(c => c.CostUsd is not null) ? group.Sum(c => c.CostUsd!.Value) : null,
                AverageDurationMs = (decimal)group.Sum(c => c.DurationMs) / group.Count(),
            };
        }).OrderByDescending(v => v.SuccessRate).ThenBy(v => v.AverageTokens).ThenBy(v => v.AverageDurationMs).ThenBy(v => v.VariantId, StringComparer.Ordinal).ToList();
        var winner = variants.Count == 0 ? null : variants[0].VariantId;
        decimal? qualityDelta = variants.Count < 2 ? null : variants[0].SuccessRate - variants[1].SuccessRate;
        decimal? costDelta = variants.Count < 2 || variants[0].TotalCostUsd is null || variants[1].TotalCostUsd is null
            ? null : variants[0].TotalCostUsd - variants[1].TotalCostUsd;
        return new BenchmarkComparisonReport
        {
            SetupId = result.SetupId, RunId = result.RunId, Winner = winner,
            WinnerReason = winner is null ? "No cases." : "Highest success rate; ties break on average tokens, duration, then variant id.",
            Variants = variants, CostDeltaUsd = costDelta, QualityDelta = qualityDelta,
        };
    }

    public static BenchmarkDefinition LoadDefinition(string path) =>
        JsonSerializer.Deserialize<BenchmarkDefinition>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"Could not deserialize benchmark setup: {path}");

    private static void Validate(BenchmarkDefinition value)
    {
        if (value.SchemaVersion != 1) throw new InvalidDataException($"Unsupported benchmark schema version {value.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(value.Id) || value.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("Setup id must be a valid file name.");
        if (value.Variants.Count < 2) throw new InvalidDataException("A benchmark requires at least two variants.");
        if (value.Repetitions < 1) throw new InvalidDataException("Repetitions must be at least one.");
        if (value.InvocationTimeoutSeconds < 1) throw new InvalidDataException("Invocation timeout must be at least one second.");
        if (value.Variants.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != value.Variants.Count) throw new InvalidDataException("Variant ids must be unique.");
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

    private static async Task<int> EvaluateAsync(BenchmarkSuccessCriteria criteria, string workspace, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(criteria.TimeoutSeconds));
        using var process = new Process { StartInfo = new(criteria.Command) { WorkingDirectory = workspace, UseShellExecute = false } };
        foreach (var argument in criteria.Arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        try { await process.WaitForExitAsync(timeout.Token); return process.ExitCode; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { process.Kill(entireProcessTree: true); return -1; }
    }

    private static async Task WriteNewAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, value, Json, cancellationToken);
    }
}

public sealed record BenchmarkRunEvent(string Name, IReadOnlyDictionary<string, object?> Context);
