using System.Diagnostics;
using System.Text.Json;
using TokenEconomy;

if (args.Length != 2 || args[0] != "run")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/TokenEconomy.Benchmarks -- run benchmarks/setups/<setup>.json");
    return 2;
}

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var setupPath = Path.GetFullPath(args[1], repositoryRoot);
var runner = new BenchmarkRunner(new CodexCliBenchmarkInvoker());
runner.EventOccurred += item => Console.Error.WriteLine(JsonSerializer.Serialize(new { item.Name, item.Context }));
var (_, report) = await runner.RunAsync(BenchmarkRunner.LoadDefinition(setupPath), repositoryRoot);
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
return report.Variants.All(v => v.Successes > 0) ? 0 : 1;

static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
    throw new DirectoryNotFoundException("Could not find TokenEconomy.slnx above the current directory.");
}

sealed class CodexCliBenchmarkInvoker : IBenchmarkInvoker
{
    public async Task<BenchmarkInvocationResponse> InvokeAsync(BenchmarkInvocationRequest request, CancellationToken cancellationToken = default)
    {
        var outputFile = Path.Combine(request.Workspace, ".benchmark-final-response.txt");
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "codex.cmd" : "codex")
        {
            WorkingDirectory = request.Workspace, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "--ask-for-approval", "never", "exec", "--json", "--ephemeral", "--skip-git-repo-check", "--sandbox", "workspace-write", "-C", request.Workspace, "-m", request.Variant.Model })
            start.ArgumentList.Add(argument);
        if (!string.IsNullOrWhiteSpace(request.Variant.ThinkingLevel))
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"model_reasoning_effort=\"{request.Variant.ThinkingLevel}\"");
        }
        start.ArgumentList.Add("--output-last-message"); start.ArgumentList.Add(outputFile);
        start.ArgumentList.Add(request.Prompt);

        using var process = new Process { StartInfo = start };
        process.Start();
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        var stdout = await stdoutTask; var stderr = await stderrTask;
        var usage = ParseUsage(stdout);
        var finalResponse = File.Exists(outputFile) ? await File.ReadAllTextAsync(outputFile, cancellationToken) : null;
        if (process.ExitCode == 0 && request.ResponseFile is not null && finalResponse is not null)
        {
            var target = Path.GetFullPath(Path.Combine(request.Workspace, request.ResponseFile));
            if (!target.StartsWith(Path.GetFullPath(request.Workspace) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Response file escapes the benchmark workspace.");
            await File.WriteAllTextAsync(target, StripCodeFence(finalResponse), cancellationToken);
        }
        return new BenchmarkInvocationResponse
        {
            ExitCode = process.ExitCode, Usage = usage,
            FinalResponse = finalResponse,
            Error = process.ExitCode == 0 ? null : Last(stderr, 4000),
        };
    }

    private static TokenUsage ParseUsage(string jsonLines)
    {
        long inputTotal = 0, output = 0, cacheRead = 0;
        foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("usage", out var usage) &&
                !(root.TryGetProperty("item", out var item) && item.TryGetProperty("usage", out usage))) continue;
            inputTotal = Math.Max(inputTotal, Number(usage, "input_tokens", "inputTokens"));
            output = Math.Max(output, Number(usage, "output_tokens", "outputTokens"));
            cacheRead = Math.Max(cacheRead, Number(usage, "cached_input_tokens", "cacheReadTokens"));
        }
        catch (JsonException) { }
        return new(Math.Max(0, inputTotal - cacheRead), output, cacheRead);
    }

    private static long Number(JsonElement value, params string[] names)
    {
        foreach (var name in names) if (value.TryGetProperty(name, out var item) && item.TryGetInt64(out var number)) return number;
        return 0;
    }
    private static string Last(string text, int length) => text.Length <= length ? text : text[^length..];
    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline ? trimmed[(firstNewline + 1)..lastFence].Trim() : trimmed;
    }
}
