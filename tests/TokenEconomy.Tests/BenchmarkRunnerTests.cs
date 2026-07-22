using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public async Task Run_creates_isolated_cases_append_only_results_and_report()
    {
        var root = Path.Combine(Path.GetTempPath(), "token-economy-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "seed"));
        await File.WriteAllTextAsync(Path.Combine(root, "seed", "original.txt"), "seed");
        try
        {
            var invoker = new RecordingInvoker();
            var runner = new BenchmarkRunner(invoker);
            var definition = Definition();
            var (result, report) = await runner.RunAsync(definition, root, "run-1");
            Assert.Equal(4, result.Cases.Count);
            Assert.All(result.Cases, item => Assert.True(item.Succeeded));
            Assert.Equal(4, invoker.Workspaces.Distinct().Count());
            Assert.Contains(report.Winner, new[] { "a", "b" });
            Assert.True(File.Exists(Path.Combine(root, "benchmarks", "results", "test", "run-1.json")));
            Assert.True(File.Exists(Path.Combine(root, "benchmarks", "results", "test", "run-1.report.json")));
            await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(definition, root, "run-1"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Compare_prioritizes_quality_then_tokens()
    {
        var result = new BenchmarkRunResult { SchemaVersion = 1, SetupId = "x", RunId = "y", StartedAtUtc = DateTime.UtcNow, CompletedAtUtc = DateTime.UtcNow,
            Cases = [Case("cheap", true, 10), Case("cheap", false, 10), Case("reliable", true, 100), Case("reliable", true, 100)] };
        var report = BenchmarkRunner.Compare(result);
        Assert.Equal("reliable", report.Winner);
        Assert.Equal(0.5m, report.QualityDelta);
    }

    private static BenchmarkDefinition Definition() => new()
    {
        SchemaVersion = 1, Id = "test", Task = new() { Prompt = "do it", SeedWorkspace = "seed" }, Repetitions = 2,
        Variants = [new() { Id = "a", Model = "model-a" }, new() { Id = "b", Model = "model-b" }],
        SuccessCriteria = new() { Command = "dotnet", Arguments = ["--version"] }
    };
    private static BenchmarkCaseResult Case(string id, bool success, long tokens) => new()
    { VariantId = id, Model = id, Repetition = 1, Succeeded = success, InvocationExitCode = 0, EvaluationExitCode = 0, Usage = new(tokens, 0), DurationMs = 1 };

    private sealed class RecordingInvoker : IBenchmarkInvoker
    {
        public List<string> Workspaces { get; } = [];
        public Task<BenchmarkInvocationResponse> InvokeAsync(BenchmarkInvocationRequest request, CancellationToken cancellationToken = default)
        {
            Workspaces.Add(request.Workspace);
            Assert.Equal("seed", File.ReadAllText(Path.Combine(request.Workspace, "original.txt")));
            File.WriteAllText(Path.Combine(request.Workspace, "changed.txt"), request.Variant.Id);
            return Task.FromResult(new BenchmarkInvocationResponse { ExitCode = 0, Usage = new(10, 2) });
        }
    }
}
