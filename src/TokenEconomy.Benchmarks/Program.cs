using System.Diagnostics;
using System.Text.Json;
using TokenEconomy;

if (args.Length != 2 || (args[0] != "run" && args[0] != "document-to-text" && args[0] != "aggregate"))
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project src/TokenEconomy.Benchmarks -- run benchmarks/setups/<setup>.json");
    Console.Error.WriteLine("  dotnet run --project src/TokenEconomy.Benchmarks -- document-to-text benchmarks/document-to-text/<corpus>.json");
    Console.Error.WriteLine("  dotnet run --project src/TokenEconomy.Benchmarks -- aggregate <agent-studio-task-storage>");
    return 2;
}

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var setupPath = Path.GetFullPath(args[1], repositoryRoot);
if (args[0] == "aggregate")
{
    var evidence = new RoutingEvidencePipeline().Run(repositoryRoot, setupPath);
    if (Environment.GetEnvironmentVariable("JOB_RESULTS_DIR") is { Length: > 0 } jobResultsDirectory)
        RoutingEvidencePipeline.WriteDerived(Path.Combine(jobResultsDirectory, "routing-evidence.json"), evidence);
    Console.WriteLine(JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    }));
    return 0;
}

if (args[0] == "document-to-text")
{
    var documentRunner = new DocumentTextBenchmarkRunner(new DocumentTextCliExtractor());
    documentRunner.EventOccurred += item => Console.Error.WriteLine(JsonSerializer.Serialize(new { item.Name, item.Context }));
    var (_, capabilityReport) = await documentRunner.RunAsync(
        DocumentTextBenchmarkRunner.LoadCorpus(setupPath), repositoryRoot);
    Console.WriteLine(JsonSerializer.Serialize(capabilityReport, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    }));
    return capabilityReport.Capabilities.Any(item => item.Level != DocumentTextCapabilityLevel.Demonstrated) ? 1 : 0;
}

var runner = new BenchmarkRunner(new ModelPrefixBenchmarkInvoker(
    new CodexCliBenchmarkInvoker(), new ClaudeCodeCliBenchmarkInvoker()));
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

sealed class ModelPrefixBenchmarkInvoker(IBenchmarkInvoker codex, IBenchmarkInvoker claude) : IBenchmarkInvoker
{
    public Task<BenchmarkInvocationResponse> InvokeAsync(
        BenchmarkInvocationRequest request, CancellationToken cancellationToken = default) =>
        request.Variant.Model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase)
            ? claude.InvokeAsync(request, cancellationToken)
            : codex.InvokeAsync(request, cancellationToken);
}

sealed class CodexCliBenchmarkInvoker : IBenchmarkInvoker
{
    public async Task<BenchmarkInvocationResponse> InvokeAsync(BenchmarkInvocationRequest request, CancellationToken cancellationToken = default)
    {
        var outputFile = Path.Combine(request.Workspace, ".benchmark-final-response.txt");
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "codex.cmd" : "codex")
        {
            WorkingDirectory = request.Workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "--ask-for-approval", "never", "exec", "--json", "--ephemeral", "--skip-git-repo-check",
            // The benchmark host is the isolation boundary for trusted checked-in fixtures. Nested
            // bubblewrap cannot create its network namespace on some workers and would otherwise
            // turn every source-reading study into an empty, invalid review.
            "--sandbox", "danger-full-access", "-C", request.Workspace, "-m", request.Variant.Model,
        })
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
            ExitCode = process.ExitCode,
            Usage = usage,
            FinalResponse = finalResponse,
            Error = process.ExitCode == 0 ? null : Last(CodexJsonDiagnostics.Error(stdout) ?? stderr, 4000),
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

static class CodexJsonDiagnostics
{
    public static string? Error(string jsonLines)
    {
        string? fallback = null;
        foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("type", out var type)
                    && type.GetString() == "turn.failed"
                    && root.TryGetProperty("error", out var failure)
                    && failure.TryGetProperty("message", out var failureMessage))
                    return failureMessage.GetString();
                if (root.TryGetProperty("type", out type)
                    && type.GetString() == "error"
                    && root.TryGetProperty("message", out var message))
                    fallback = message.GetString();
                if (root.TryGetProperty("item", out var item)
                    && item.TryGetProperty("type", out var itemType)
                    && itemType.GetString() == "error"
                    && item.TryGetProperty("message", out var itemMessage))
                    fallback ??= itemMessage.GetString();
            }
            catch (JsonException) { }
        return fallback;
    }
}

sealed class ClaudeCodeCliBenchmarkInvoker : IBenchmarkInvoker
{
    public async Task<BenchmarkInvocationResponse> InvokeAsync(
        BenchmarkInvocationRequest request, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "claude.cmd" : "claude")
        {
            WorkingDirectory = request.Workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "--print", "--output-format", "json", "--model", request.Variant.Model,
            "--permission-mode", "bypassPermissions", "--no-session-persistence",
        })
            start.ArgumentList.Add(argument);
        if (!string.IsNullOrWhiteSpace(request.Variant.ThinkingLevel))
        {
            start.ArgumentList.Add("--effort");
            start.ArgumentList.Add(request.Variant.ThinkingLevel);
        }
        start.ArgumentList.Add(request.Prompt);

        using var process = new Process { StartInfo = start };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var parsed = Parse(stdout);
        var exitCode = process.ExitCode == 0 && parsed.IsError ? 1 : process.ExitCode;
        if (exitCode == 0 && request.ResponseFile is not null && parsed.FinalResponse is not null)
        {
            var target = Path.GetFullPath(Path.Combine(request.Workspace, request.ResponseFile));
            if (!target.StartsWith(Path.GetFullPath(request.Workspace) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Response file escapes the benchmark workspace.");
            await File.WriteAllTextAsync(target, StripCodeFence(parsed.FinalResponse), cancellationToken);
        }
        return new()
        {
            ExitCode = exitCode,
            Usage = parsed.Usage,
            CostUsd = parsed.CostUsd,
            FinalResponse = parsed.FinalResponse,
            Error = exitCode == 0 ? null : Last(
                string.IsNullOrWhiteSpace(stderr) ? parsed.FinalResponse ?? "Claude Code invocation failed." : stderr,
                4000),
        };
    }

    private static (TokenUsage Usage, decimal? CostUsd, string? FinalResponse, bool IsError) Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (default, null, null, false);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var usage = root.TryGetProperty("usage", out var usageJson)
                ? new TokenUsage(
                    Number(usageJson, "input_tokens"),
                    Number(usageJson, "output_tokens"),
                    Number(usageJson, "cache_read_input_tokens"),
                    Number(usageJson, "cache_creation_input_tokens"))
                : default;
            decimal? cost = root.TryGetProperty("total_cost_usd", out var costJson)
                && costJson.TryGetDecimal(out var amount) ? amount : null;
            var response = root.TryGetProperty("result", out var result) ? result.GetString() : null;
            var isError = root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            return (usage, cost, response, isError);
        }
        catch (JsonException)
        {
            return (default, null, null, false);
        }
    }

    private static long Number(JsonElement value, string name) =>
        value.TryGetProperty(name, out var item) && item.TryGetInt64(out var number) ? number : 0;

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

sealed class DocumentTextCliExtractor : IDocumentTextExtractor
{
    public async Task<DocumentTextExtractionResponse> ExtractAsync(
        DocumentTextExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var isClaude = request.Model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase);
        return isClaude
            ? await InvokeClaudeAsync(request, cancellationToken)
            : await InvokeCodexAsync(request, cancellationToken);
    }

    private static async Task<DocumentTextExtractionResponse> InvokeCodexAsync(
        DocumentTextExtractionRequest request, CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(Path.GetTempPath(), $"token-economy-document-text-{Guid.NewGuid():N}.txt");
        try
        {
            var directory = Path.GetDirectoryName(request.DocumentPath)!;
            var start = Redirected(OperatingSystem.IsWindows() ? "codex.cmd" : "codex", directory);
            foreach (var argument in new[]
            {
                "--ask-for-approval", "never", "exec", "--json", "--ephemeral", "--skip-git-repo-check",
                // The benchmark host is the isolation boundary for this trusted, checked-in corpus.
                // A nested Codex sandbox is not portable to Windows clean homes or Linux workers
                // that cannot create a second network namespace.
                "--sandbox", "danger-full-access", "-C", directory, "-m", request.Model,
                "--output-last-message", outputFile, Prompt(Path.GetFileName(request.DocumentPath)),
            })
                start.ArgumentList.Add(argument);

            var processResult = await RunAsync(start, cancellationToken);
            var text = File.Exists(outputFile) ? await File.ReadAllTextAsync(outputFile, cancellationToken) : null;
            var infrastructureError = CodexInfrastructureError(processResult.Stdout, processResult.Stderr);
            var exitCode = processResult.ExitCode == 0 && infrastructureError is not null
                ? -1
                : processResult.ExitCode;
            return new()
            {
                ExitCode = exitCode,
                Usage = ParseCodexUsage(processResult.Stdout),
                ExtractedText = text,
                Error = exitCode == 0 ? null
                    : Last(CodexJsonDiagnostics.Error(processResult.Stdout)
                        ?? infrastructureError ?? processResult.Stderr, 4000),
            };
        }
        finally
        {
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    private static async Task<DocumentTextExtractionResponse> InvokeClaudeAsync(
        DocumentTextExtractionRequest request, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(request.DocumentPath)!;
        var start = Redirected(OperatingSystem.IsWindows() ? "claude.cmd" : "claude", directory);
        foreach (var argument in new[]
        {
            "--print", "--output-format", "json", "--model", request.Model,
            "--permission-mode", "bypassPermissions", "--no-session-persistence",
            Prompt(Path.GetFileName(request.DocumentPath)),
        })
            start.ArgumentList.Add(argument);

        var processResult = await RunAsync(start, cancellationToken);
        string? text = null;
        var usage = default(TokenUsage);
        var isError = false;
        try
        {
            using var document = JsonDocument.Parse(processResult.Stdout);
            var root = document.RootElement;
            text = root.TryGetProperty("result", out var result) ? result.GetString() : null;
            isError = root.TryGetProperty("is_error", out var error)
                && error.ValueKind == JsonValueKind.True;
            if (root.TryGetProperty("usage", out var usageJson))
                usage = new(
                    Number(usageJson, "input_tokens"),
                    Number(usageJson, "output_tokens"),
                    Number(usageJson, "cache_read_input_tokens"),
                    Number(usageJson, "cache_creation_input_tokens"));
        }
        catch (JsonException) when (processResult.ExitCode != 0) { }
        var exitCode = processResult.ExitCode == 0 && isError ? 1 : processResult.ExitCode;
        return new()
        {
            ExitCode = exitCode,
            Usage = usage,
            ExtractedText = text,
            Error = exitCode == 0 ? null : Last(
                string.IsNullOrWhiteSpace(processResult.Stderr)
                    ? text ?? "Claude Code invocation failed."
                    : processResult.Stderr,
                4000),
        };
    }

    private static string Prompt(string fileName) =>
        $"Extract visible document content from {fileName} into plain text. Preserve the human reading order, " +
        "tables as readable rows, and visible headers/footers. Exclude metadata, hidden content, deleted revisions, " +
        "hidden worksheets, and speaker notes. Return only the extracted text with no Markdown fence or explanation.";

    private static ProcessStartInfo Redirected(string command, string workingDirectory) => new(command)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    private static string? CodexInfrastructureError(string stdout, string stderr)
    {
        var diagnostics = stdout + "\n" + stderr;
        foreach (var marker in new[]
        {
            "Refusing to create helper binaries under temporary dir",
            "fs sandbox helper failed",
            "bwrap: loopback: Failed RTM_NEWADDR",
        })
            if (diagnostics.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return marker;
        return null;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        ProcessStartInfo start, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = start };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static TokenUsage ParseCodexUsage(string jsonLines)
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
        foreach (var name in names)
            if (value.TryGetProperty(name, out var item) && item.TryGetInt64(out var number)) return number;
        return 0;
    }

    private static string Last(string text, int length) => text.Length <= length ? text : text[^length..];
}
