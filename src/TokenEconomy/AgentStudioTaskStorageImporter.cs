using System.Diagnostics;
using System.Security.Cryptography;
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
/// lastUsage, taskType, prompt/card features, final lane, project, and timestamps. Unknown fields are ignored for forwards compatibility.
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
        foreach (var path in Directory.EnumerateFiles(storageDirectory, "task.json", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            files++;
            try
            {
                var bytes = File.ReadAllBytes(path);
                using var document = JsonDocument.Parse(bytes);
                var reference = "agent-studio-task-storage/" + Path.GetRelativePath(storageDirectory, path).Replace('\\', '/');
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                foreach (var record in ParseRecords(document.RootElement))
                {
                    destination.Upsert(record with { ProvenanceReference = reference, ProvenanceSha256 = hash });
                    upserted++;
                }
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

    /// <summary>
    /// Maps one task.json document to attempt records. Attempt-local routes and measurements win;
    /// card-level route fields are used only when the document has no attempt history.
    /// </summary>
    public IReadOnlyList<AgentStudioRunRecord> ParseRecords(JsonElement root)
    {
        var task = Object(root, "task") ?? root;
        var taskKey = Text(task, "taskKey", "key", "id") ?? throw new InvalidDataException("Agent Studio task.json has no task key.");
        var attempts = Array(task, "attempts", "runAttempts", "attemptHistory", "runHistory", "runs");
        if (attempts is null)
            return [ParseRecord(task, task, null, taskKey, AgentStudioRouteGranularity.Card)];

        var records = attempts.Value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select((attempt, index) => ParseRecord(task, attempt,
                Object(attempt, "route") ?? Object(attempt, "selectedRoute") ?? Object(attempt, "routing"),
                taskKey, AgentStudioRouteGranularity.Attempt, index + 1))
            .GroupBy(record => record.Run)
            .Select(MergeAttemptDuplicates)
            .OrderBy(record => record.Run)
            .ToArray();
        return records;
    }

    /// <summary>
    /// Maps a task.json document to its latest attempt. Use <see cref="ParseRecords"/> when attempt
    /// history is available and every route must be retained.
    /// </summary>
    public AgentStudioRunRecord Parse(JsonElement root)
    {
        var records = ParseRecords(root);
        if (records.Count == 0) throw new InvalidDataException("Agent Studio task.json has no importable attempt.");
        return records.OrderBy(record => record.Run).Last();
    }

    private AgentStudioRunRecord ParseRecord(
        JsonElement task,
        JsonElement measurement,
        JsonElement? route,
        string taskKey,
        AgentStudioRouteGranularity requestedGranularity,
        int fallbackRun = 0)
    {
        // These are card-authored intake facts. Do not map post-run changedFiles/diffStats here:
        // eventual implementation scope would leak an outcome into an upfront estimate.
        var routing = Object(task, "routingFeatures") ?? Object(task, "upfrontComplexity")
            ?? Object(task, "complexityRoutingSignals") ?? Object(task, "complexity");
        var model = Text(route, "model", "modelId") ?? Text(measurement, "model", "modelId");
        if (requestedGranularity == AgentStudioRouteGranularity.Card)
            model ??= Text(task, "model", "modelId");
        var thinking = Text(route, "thinkingLevel", "effort", "reasoningEffort")
            ?? Text(measurement, "thinkingLevel", "effort", "reasoningEffort");
        if (requestedGranularity == AgentStudioRouteGranularity.Card)
            thinking ??= Text(task, "thinkingLevel", "effort", "reasoningEffort");
        var granularity = string.IsNullOrWhiteSpace(model) ? AgentStudioRouteGranularity.Unknown : requestedGranularity;
        // Price at execution, not at a later card update. Keep the update timestamp separately so
        // consumers can track when this record was observed without changing its historic cost.
        // UnixEpoch remains the stable "unknown" timestamp so undated cards stay idempotent.
        var executedAt = Date(measurement, "completedAt", "finishedAt", "updatedAt", "createdAt")
            ?? Date(task, "completedAt", "finishedAt", "updatedAt", "createdAt") ?? DateTime.UnixEpoch;
        var observedAt = Date(measurement, "updatedAt", "completedAt", "finishedAt", "createdAt")
            ?? Date(task, "updatedAt", "completedAt", "finishedAt", "createdAt") ?? executedAt;
        var usageElement = Object(measurement, "tokenSummary") ?? Object(measurement, "lastUsage");
        var usage = Usage(usageElement);
        var cost = _prices.ComputeCost(model, usage, executedAt);
        var lane = Text(measurement, "finalLane", "lane", "column");
        if (requestedGranularity == AgentStudioRouteGranularity.Card)
            lane ??= Text(task, "finalLane", "lane", "column");
        var listing = _prices.Find(model);
        return new AgentStudioRunRecord
        {
            TaskKey = taskKey, Run = Number(measurement, fallbackRun, "run", "attempt", "runNumber"), Project = Text(task, "project", "projectId"),
            Provider = listing?.Vendor ?? ProviderFromCli(Text(route, "cliType") ?? Text(measurement, "cliType") ?? Text(task, "cliType")),
            Model = cost.ModelId ?? model, ThinkingLevel = thinking, RouteGranularity = granularity,
            CliType = Text(route, "cliType") ?? Text(measurement, "cliType") ?? Text(task, "cliType"), TaskType = Text(task, "taskType"),
            Capability = Text(task, "capability", "requiredCapability"),
            TaskPrompt = Text(task, "prompt", "description"), Area = Text(task, "area", "component"),
            EpicContext = Text(task, "epicContext", "epic"), AcceptanceCriteria = Strings(task, "acceptanceCriteria", "criteria"),
            ReferencedFiles = Strings(routing, task, "referencedFiles", "expectedFiles"),
            ReferencedSubsystems = Strings(routing, task, "referencedSubsystems", "expectedSubsystems"),
            ExpectedChangedLines = NullableNumber(routing, task, "expectedChangedLines", "expectedLineCount"),
            DependencyFanOut = NullableNumber(task, "dependencyFanOut"), RepositoryFileCount = NullableNumber(task, "repositoryFileCount"),
            RoutingSignals = new ComplexityRoutingSignals
            {
                CorrectnessRisk = NullableDouble(routing, task, "correctnessRisk", "correctnessRiskScore"),
                ExpectedScope = NullableDouble(routing, task, "expectedScope", "expectedScopeScore"),
                ContextDemand = NullableDouble(routing, task, "contextDemand", "contextDemandScore"),
                TaskUncertainty = NullableDouble(routing, task, "taskUncertainty", "taskTypeAndUncertainty", "taskUncertaintyScore"),
                QuotaAndCostHeadroom = NullableDouble(routing, task, "quotaAndCostHeadroom", "quotaHeadroom", "quotaAndCostHeadroomScore"),
            },
            HardFloorTriggers = HardFloorTriggers(routing, task),
            FinalLane = lane,
            Usage = usage, TokenUsageAvailable = usageElement is not null, ExecutedAtUtc = executedAt,
            CostEstimate = usageElement is null ? null : cost.Total, Currency = usageElement is null ? null : cost.Currency,
            CostStatus = usageElement is null ? PriceStatus.UsageUnavailable : cost.Status,
            CostCaveat = usageElement is null ? null : cost.Caveat, Outcome = Outcome(lane),
            Grade = Grade(Text(measurement, "grade", "reviewGrade", "finalGrade")),
            SemanticReissue = Boolean(measurement, "semanticReissue", "isSemanticReissue"),
            StartedAtUtc = Date(measurement, "startedAt", "createdAt"), ObservedAtUtc = observedAt,
        };
    }

    private static AgentStudioRunRecord MergeAttemptDuplicates(IGrouping<int, AgentStudioRunRecord> group)
    {
        var ordered = group.OrderByDescending(record => record.ObservedAtUtc).ToArray();
        var newest = ordered[0];
        var peers = ordered.Where(record => record.ObservedAtUtc == newest.ObservedAtUtc).ToArray();
        var routeAmbiguous = peers.Select(record => (record.Model, record.ThinkingLevel)).Distinct().Count() > 1;
        return routeAmbiguous
            ? newest with { Model = null, ThinkingLevel = null, Provider = null, RouteGranularity = AgentStudioRouteGranularity.Unknown,
                CostEstimate = null, Currency = null, CostCaveat = null, CostStatus = PriceStatus.UnknownModel }
            : newest;
    }

    private static JsonElement? Object(JsonElement value, string name) => Property(value, name) is { ValueKind: JsonValueKind.Object } result ? result : null;
    private static JsonElement? Object(JsonElement? value, string name) => value is { } item ? Object(item, name) : null;
    private static JsonElement? Array(JsonElement value, params string[] names)
    {
        foreach (var name in names)
            if (Property(value, name) is { ValueKind: JsonValueKind.Array } result) return result;
        return null;
    }
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
    private static string? Text(JsonElement? value, params string[] names) => value is { } item ? Text(item, names) : null;
    private static int Number(JsonElement value, params string[] names)
        => int.TryParse(Text(value, names), out var number) ? number : 0;
    private static int Number(JsonElement value, int fallback, params string[] names)
        => int.TryParse(Text(value, names), out var number) ? number : fallback;
    private static int? NullableNumber(JsonElement value, params string[] names)
        => int.TryParse(Text(value, names), out var number) ? Math.Max(0, number) : null;
    private static int? NullableNumber(JsonElement? preferred, JsonElement fallback, params string[] names)
        => preferred is { } value && NullableNumber(value, names) is { } number ? number : NullableNumber(fallback, names);
    private static double? NullableDouble(JsonElement value, params string[] names)
        => double.TryParse(Text(value, names), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;
    private static double? NullableDouble(JsonElement? preferred, JsonElement fallback, params string[] names)
        => preferred is { } value && NullableDouble(value, names) is { } number ? number : NullableDouble(fallback, names);
    private static IReadOnlyList<string> Strings(JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (Property(value, name) is not { } item) continue;
            if (item.ValueKind == JsonValueKind.Array)
                return item.EnumerateArray().Where(v => v.ValueKind is JsonValueKind.String or JsonValueKind.Number).Select(v => v.ToString()).ToArray();
            if (item.ValueKind == JsonValueKind.String) return [item.GetString()!];
        }
        return [];
    }
    private static IReadOnlyList<string> Strings(JsonElement? preferred, JsonElement fallback, params string[] names)
    {
        var values = preferred is { } value ? Strings(value, names) : [];
        return values.Count > 0 ? values : Strings(fallback, names);
    }
    private static IReadOnlyList<ComplexityHardFloorTrigger> HardFloorTriggers(JsonElement? preferred, JsonElement fallback)
    {
        var values = preferred is { } value ? Strings(value, "hardFloorTriggers", "hardFloors") : [];
        if (values.Count == 0) values = Strings(fallback, "hardFloorTriggers", "hardFloors");
        return values.Select(item => new string(item.Where(char.IsLetterOrDigit).ToArray()))
            .Select(item => Enum.TryParse<ComplexityHardFloorTrigger>(item, true, out var trigger)
                ? (ComplexityHardFloorTrigger?)trigger : null)
            .Where(trigger => trigger is not null)
            .Select(trigger => trigger!.Value)
            .Distinct()
            .Order()
            .ToArray();
    }
    private static DateTime? Date(JsonElement value, params string[] names)
        => DateTime.TryParse(Text(value, names), null, System.Globalization.DateTimeStyles.RoundtripKind, out var date) ? date.ToUniversalTime() : null;
    private static bool? Boolean(JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (Property(value, name) is { ValueKind: JsonValueKind.True }) return true;
            if (Property(value, name) is { ValueKind: JsonValueKind.False }) return false;
        }
        return null;
    }
    private static long Tokens(JsonElement? usage, params string[] names)
        => usage is { } value && long.TryParse(Text(value, names), out var count) ? Math.Max(0, count) : 0;
    private static TokenUsage Usage(JsonElement? usage) => new(
        Tokens(usage, "inputTokens", "input", "promptTokens"), Tokens(usage, "outputTokens", "output", "completionTokens"),
        Tokens(usage, "cacheReadTokens", "cacheRead"), Tokens(usage, "cacheWriteTokens", "cacheWrite"));
    private static string? ProviderFromCli(string? cli) => cli?.ToLowerInvariant() switch { "claude" => "anthropic", "codex" => "openai", _ => null };
    private static string? Grade(string? grade) => grade?.Trim().ToUpperInvariant() switch
    {
        "A" => "A", "B" => "B", "C" => "C", "D" => "D", _ => null,
    };
    private static OutcomeQualitySignal Outcome(string? lane)
    {
        var value = lane?.ToLowerInvariant() ?? "";
        if (value.Contains("done") || value.Contains("complete") || value.Contains("merged")) return OutcomeQualitySignal.Successful;
        if (value.Contains("fail") || value.Contains("cancel") || value.Contains("reject")) return OutcomeQualitySignal.Unsuccessful;
        if (value.Contains("review") || value.Contains("block")) return OutcomeQualitySignal.NeedsReview;
        return OutcomeQualitySignal.Unknown;
    }
}
