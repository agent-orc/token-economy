using System.Diagnostics;
using System.Text.Json;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Counts returned after an Agent Studio task-storage import.</summary>
public sealed record AgentStudioImportResult(int FilesRead, int RecordsUpserted, TimeSpan Elapsed);

/// <summary>Structured, dependency-free import event for host logging.</summary>
public sealed record AgentStudioImportEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>
/// Imports Agent Studio's on-disk task storage. The contract is a <c>task.json</c> per card, read
/// directly (not through task-server): this remains available to batch/reporting jobs when no server
/// is running. Fields read are task key, run/attempt, model, thinkingLevel, cliType, tokenSummary,
/// lastUsage, taskType, final lane, project, and timestamps. Unknown fields are ignored for forwards compatibility.
/// </summary>
public sealed class AgentStudioTaskStorageImporter
{
    private readonly ModelPriceCatalog _prices;
    public AgentStudioTaskStorageImporter(ModelPriceCatalog? prices = null) => _prices = prices ?? ModelPriceCatalog.Default;
    /// <summary>Raised after a completed import; hosts can route it to their structured logger.</summary>
    public event Action<AgentStudioImportEvent>? EventOccurred;

    /// <summary>Recursively imports every <c>task.json</c> below <paramref name="storageDirectory"/> and upserts by task key + run.</summary>
    public AgentStudioImportResult ImportDirectory(string storageDirectory, IAgentStudioRunStore destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        ArgumentNullException.ThrowIfNull(destination);
        var timer = Stopwatch.StartNew(); var files = 0; var upserted = 0;
        foreach (var path in Directory.EnumerateFiles(storageDirectory, "task.json", SearchOption.AllDirectories))
        {
            files++;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                destination.Upsert(Parse(document.RootElement)); upserted++;
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidDataException)
            {
                EventOccurred?.Invoke(new("agent_studio.task_storage.import_failed", new Dictionary<string, object?>
                {
                    ["path"] = path, ["errorType"] = error.GetType().Name, ["elapsedMs"] = timer.ElapsedMilliseconds,
                }));
                throw;
            }
        }
        timer.Stop();
        var result = new AgentStudioImportResult(files, upserted, timer.Elapsed);
        EventOccurred?.Invoke(new("agent_studio.task_storage.imported", new Dictionary<string, object?>
        { ["filesRead"] = files, ["recordsUpserted"] = upserted, ["elapsedMs"] = timer.ElapsedMilliseconds }));
        return result;
    }

    /// <summary>Maps one task.json document to a priced run record. Exposed for API-based callers that already obtained the JSON.</summary>
    public AgentStudioRunRecord Parse(JsonElement root)
    {
        var task = Object(root, "task") ?? root;
        var model = Text(task, "model") ?? throw new InvalidDataException("Agent Studio task.json has no model.");
        var taskKey = Text(task, "taskKey", "key", "id") ?? throw new InvalidDataException("Agent Studio task.json has no task key.");
        var observed = Date(task, "completedAt", "updatedAt", "finishedAt", "createdAt") ?? DateTime.UtcNow;
        var usage = Usage(Object(task, "tokenSummary") ?? Object(task, "lastUsage"));
        var cost = _prices.ComputeCost(model, usage, observed);
        var lane = Text(task, "finalLane", "lane", "column");
        var listing = _prices.Find(model);
        return new AgentStudioRunRecord
        {
            TaskKey = taskKey, Run = Number(task, "run", "attempt", "runNumber"), Project = Text(task, "project", "projectId"),
            Provider = listing?.Vendor ?? ProviderFromCli(Text(task, "cliType")), Model = cost.ModelId ?? model,
            ThinkingLevel = Text(task, "thinkingLevel"), CliType = Text(task, "cliType"), TaskType = Text(task, "taskType"), FinalLane = lane,
            Usage = usage, ExecutedAtUtc = observed, CostEstimate = cost.Total, Currency = cost.Currency, CostStatus = cost.Status, CostCaveat = cost.Caveat, Outcome = Outcome(lane),
            StartedAtUtc = Date(task, "startedAt", "createdAt"), ObservedAtUtc = observed,
        };
    }

    private static JsonElement? Object(JsonElement value, string name) => Property(value, name) is { ValueKind: JsonValueKind.Object } result ? result : null;
    private static JsonElement? Property(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in value.EnumerateObject()) if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }
    private static string? Text(JsonElement value, params string[] names)
    {
        foreach (var name in names) if (Property(value, name) is { } item && item.ValueKind is JsonValueKind.String or JsonValueKind.Number) return item.ToString();
        return null;
    }
    private static int Number(JsonElement value, params string[] names)
        => int.TryParse(Text(value, names), out var number) ? number : 0;
    private static DateTime? Date(JsonElement value, params string[] names)
        => DateTime.TryParse(Text(value, names), null, System.Globalization.DateTimeStyles.RoundtripKind, out var date) ? date.ToUniversalTime() : null;
    private static long Tokens(JsonElement? usage, params string[] names)
        => usage is { } value && long.TryParse(Text(value, names), out var count) ? Math.Max(0, count) : 0;
    private static TokenUsage Usage(JsonElement? usage) => new(
        Tokens(usage, "inputTokens", "input", "promptTokens"), Tokens(usage, "outputTokens", "output", "completionTokens"),
        Tokens(usage, "cacheReadTokens", "cacheRead"), Tokens(usage, "cacheWriteTokens", "cacheWrite"));
    private static string? ProviderFromCli(string? cli) => cli?.ToLowerInvariant() switch { "claude" => "anthropic", "codex" => "openai", _ => null };
    private static OutcomeQualitySignal Outcome(string? lane)
    {
        var value = lane?.ToLowerInvariant() ?? "";
        if (value.Contains("done") || value.Contains("complete") || value.Contains("merged")) return OutcomeQualitySignal.Successful;
        if (value.Contains("fail") || value.Contains("cancel") || value.Contains("reject")) return OutcomeQualitySignal.Unsuccessful;
        if (value.Contains("review") || value.Contains("block")) return OutcomeQualitySignal.NeedsReview;
        return OutcomeQualitySignal.Unknown;
    }
}
