using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TokenEconomy;

public static class BenchmarkCliTransport
{
    public static ProcessStartInfo Create(string provider, string workspace)
    {
        var start = new ProcessStartInfo(provider)
        {
            WorkingDirectory = workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!OperatingSystem.IsWindows()) return start;
        var npm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules");
        if (provider == "claude")
        {
            var executable = Path.Combine(npm, "@anthropic-ai", "claude-code", "bin", "claude.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Native Claude executable not found; benchmark will not invoke a command shell.", executable);
            start.FileName = executable;
        }
        else
        {
            var entry = Path.Combine(npm, "@openai", "codex", "bin", "codex.js");
            if (!File.Exists(entry)) throw new FileNotFoundException("Codex Node entry point not found; benchmark will not invoke a command shell.", entry);
            start.FileName = "node";
            start.ArgumentList.Add(entry);
        }
        return start;
    }

    public static IReadOnlyList<string> ReportedModels(string json)
    {
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = new List<string> { json };
        documents.AddRange(json.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        foreach (var line in documents)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
                    models.Add(model.GetString()!);
                if (root.TryGetProperty("modelUsage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                    foreach (var item in usage.EnumerateObject()) models.Add(item.Name);
            }
            catch (JsonException) { }
        }
        return models.Order(StringComparer.Ordinal).ToArray();
    }

    public static bool MatchesRequested(string requested, IReadOnlyList<string> reported)
    {
        var key = requested.ToLowerInvariant().Replace('.', '-');
        return reported.Count == 0 || reported.Any(model =>
        {
            var value = model.ToLowerInvariant().Replace('.', '-');
            return value == key || value.StartsWith(key + "-", StringComparison.Ordinal);
        });
    }

    public static void Save(BenchmarkInvocationRequest request, string provider, string stdout,
        string? response, int exitCode, string? error, long durationMs, TokenUsage? usage = null, decimal? costUsd = null)
    {
        var directory = Environment.GetEnvironmentVariable("TOKEN_ECONOMY_BENCHMARK_TRANSPORT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var run = new DirectoryInfo(request.Workspace).Parent!.Parent!.Name;
        var stem = Path.Combine(directory, request.SetupId, run, request.Variant.Id + "-" + request.Repetition);
        Directory.CreateDirectory(Path.GetDirectoryName(stem)!);
        var models = ReportedModels(stdout);
        var metadata = new
        {
            schemaVersion = 1, request.SetupId, runId = run,
            requestedModel = request.Variant.Model, requestedEffort = request.Variant.ThinkingLevel,
            providerReportedModels = models, actualModelExposed = models.Count > 0,
            requestedModelObserved = models.Count == 0 ? (bool?)null : MatchesRequested(request.Variant.Model, models),
            provider, recordedAtUtc = DateTime.UtcNow, durationMs, exitCode, error, usage, reportedCostUsd = costUsd,
            responseSha256 = response is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response))).ToLowerInvariant(),
        };
        using (var stream = new FileStream(stem + ".transport.json", FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(stream, metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        if (response is not null)
        {
            using var writer = new StreamWriter(new FileStream(stem + ".response.txt", FileMode.CreateNew, FileAccess.Write), Encoding.UTF8);
            writer.Write(response);
        }
    }
}
