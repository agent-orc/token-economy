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
/// lastUsage, routing decision, actual route, outcome/review/reissue classification, taskType,
/// prompt/card features, final lane, project, and timestamps. Unknown fields are ignored for forwards compatibility.
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
                foreach (var parsed in ParseRecords(document.RootElement))
                {
                    var record = parsed with
                    {
                        ProvenanceReference = reference,
                        ProvenanceSha256 = hash,
                        RoutingDecision = parsed.RoutingDecision is { } decision
                            ? decision with { ProvenanceReference = reference, ProvenanceSha256 = hash }
                            : null,
                        OutcomeObservation = parsed.OutcomeObservation is { } observation
                            ? observation with { ProvenanceReference = reference, ProvenanceSha256 = hash }
                            : null,
                    };
                    destination.Upsert(record);
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
        var run = Number(measurement, fallbackRun, "run", "attempt", "runNumber");
        var decision = Object(measurement, "routingDecision") ?? Object(measurement, "modelRoutingDecision")
            ?? Object(measurement, "decision");
        var recommendedRoute = Object(decision, "recommendedRoute") ?? Object(decision, "recommended");
        var selectedRoute = Object(decision, "selectedRoute") ?? Object(decision, "selected");
        var correctnessFloor = Object(decision, "correctnessFloor") ?? Object(decision, "hardFloor");
        var operatorPin = Object(decision, "operatorPin");
        var configuredRoute = Object(decision, "configuredRoute") ?? Object(decision, "cardConfiguredRoute");
        var quotaSnapshot = Object(decision, "quotaSnapshot");
        route ??= selectedRoute;
        var model = Text(measurement, "actualModel", "executedModel")
            ?? Text(route, "model", "modelId") ?? Text(measurement, "model", "modelId");
        if (requestedGranularity == AgentStudioRouteGranularity.Card)
            model ??= Text(task, "model", "modelId");
        var thinking = Text(measurement, "actualThinkingLevel", "actualEffort")
            ?? Text(route, "thinkingLevel", "effort", "reasoningEffort")
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
        var grade = Grade(Text(measurement, "grade", "reviewGrade", "finalGrade"));
        var rawReviewOutcome = Text(measurement, "reviewOutcome", "reviewResult");
        var reviewOutcome = ReviewOutcome(rawReviewOutcome, grade);
        var rawOutcome = Text(measurement, "outcomeCategory", "outcome", "attemptOutcome", "failureCategory", "status", "result");
        var reissueReason = Text(measurement, "reissueReason", "retryReason", "reissueCategory", "failureReason");
        var explicitSemanticReissue = Boolean(measurement, "semanticReissue", "isSemanticReissue");
        var category = OutcomeCategory(rawOutcome, reissueReason, grade, explicitSemanticReissue, lane);
        var decisionId = Text(decision, "decisionId", "id", "routingDecisionId")
            ?? Text(measurement, "routingDecisionId", "decisionId")
            ?? $"{taskKey}:attempt:{run}:routing";
        var observationId = Text(measurement, "outcomeObservationId", "observationId")
            ?? StableId($"{taskKey}\n{run}\n{measurement.GetRawText()}");
        var duration = Date(measurement, "startedAt", "createdAt") is { } started && executedAt >= started
            ? (long?)(executedAt - started).TotalMilliseconds : null;
        var disposition = EnumValue<ModelRoutingDisposition>(Text(decision, "disposition"));
        var selectionSource = Text(decision, "selectionSource", "source");
        var fallbackOrWaitReason = Text(decision, "fallbackOrWaitReason");
        var quotaFallback = Boolean(decision, "quotaFallbackApplied")
            ?? (Normalize(selectionSource) is "equivalentproviderfallback" or "onetierquotadowngrade" ? true : null);
        var decisionRecord = new AgentStudioRoutingDecisionRecord
        {
            DecisionId = decisionId,
            TaskKey = taskKey,
            Run = run,
            Disposition = disposition,
            PolicyVersion = Text(decision, "policyVersion") ?? Text(route, "policyVersion") ?? Text(measurement, "policyVersion"),
            RecommendedRouteId = Text(recommendedRoute, "routeId", "id") ?? Text(decision, "recommendedRouteId"),
            RecommendedModel = Text(recommendedRoute, "model", "modelId") ?? Text(decision, "recommendedModel"),
            RecommendedThinkingLevel = Text(recommendedRoute, "thinkingLevel", "effort", "reasoningEffort")
                ?? Text(decision, "recommendedThinkingLevel"),
            RecommendedRouteProvisional = Boolean(recommendedRoute, "provisional")
                ?? Boolean(decision, "recommendedRouteProvisional", "provisional"),
            SelectedRouteId = Text(selectedRoute, "routeId", "id") ?? Text(decision, "selectedRouteId"),
            SelectedModel = Text(selectedRoute, "model", "modelId") ?? Text(decision, "selectedModel") ?? Text(route, "model", "modelId"),
            SelectedThinkingLevel = Text(selectedRoute, "thinkingLevel", "effort", "reasoningEffort")
                ?? Text(decision, "selectedThinkingLevel") ?? Text(route, "thinkingLevel", "effort", "reasoningEffort"),
            SelectedRouteProvisional = Boolean(selectedRoute, "provisional") ?? Boolean(decision, "selectedRouteProvisional"),
            SelectionSource = selectionSource,
            Score = NullableNumber(decision, "effectivePolicyScore", "score"),
            UpfrontScore = NullableNumber(decision, "upfrontScore", "intakeScore"),
            HardFloorRouteId = Text(correctnessFloor, "routeId") ?? Text(decision, "hardFloorRouteId"),
            HardFloorModel = Text(correctnessFloor, "model", "modelId") ?? Text(decision, "hardFloorModel"),
            HardFloorThinkingLevel = Text(correctnessFloor, "thinkingLevel", "effort") ?? Text(decision, "hardFloorThinkingLevel"),
            AppliedHardFloorIds = Strings(correctnessFloor, decision ?? measurement, "appliedFloorIds", "appliedHardFloorIds"),
            IsHardFloor = Boolean(correctnessFloor, "isHardFloor") ?? Boolean(decision, "isHardFloor"),
            SemanticPromotionApplied = Boolean(decision, "semanticPromotionApplied", "reissuePromoted"),
            ConfiguredModel = Text(configuredRoute, "model", "modelId") ?? Text(decision, "configuredModel"),
            ConfiguredThinkingLevel = Text(configuredRoute, "thinkingLevel", "effort") ?? Text(decision, "configuredThinkingLevel"),
            OperatorPinModel = Text(operatorPin, "model", "modelId") ?? Text(decision, "operatorPinModel"),
            OperatorPinThinkingLevel = Text(operatorPin, "thinkingLevel", "effort") ?? Text(decision, "operatorPinThinkingLevel"),
            OperatorPinBelowPolicy = Boolean(decision, "operatorPinBelowPolicy", "pinBelowPolicy"),
            OperatorPinWarning = Text(decision, "operatorPinWarning", "pinWarning"),
            QuotaFallbackApplied = quotaFallback,
            QuotaFallbackReason = Text(decision, "quotaFallbackReason")
                ?? (quotaFallback == true ? fallbackOrWaitReason : null),
            WaitOrOverrideReason = Text(decision, "waitOrOverrideReason", "waitReason")
                ?? (disposition is ModelRoutingDisposition.Wait or ModelRoutingDisposition.OverrideRequired
                    ? fallbackOrWaitReason : null),
            QuotaSnapshotDecisionAtUtc = quotaSnapshot is { } snapshot
                ? Date(snapshot, "decisionAtUtc", "decisionAt")
                : Date(decision, "quotaSnapshotDecisionAtUtc"),
            QuotaSnapshotId = Text(quotaSnapshot, "snapshotId", "id") ?? Text(decision, "quotaSnapshotId"),
            PolicyReason = Text(decision, "policyReason"),
            Reason = Text(decision, "reason", "policyReason", "fallbackOrWaitReason"),
            DecidedAtUtc = decision is { } decisionValue ? Date(decisionValue, "decidedAtUtc", "decidedAt", "decisionAt", "createdAt") : null,
        };
        var observation = new AgentStudioRunOutcomeObservation
        {
            ObservationId = observationId,
            DecisionId = decisionId,
            TaskKey = taskKey,
            Run = run,
            ActualModel = cost.ModelId ?? model,
            ActualThinkingLevel = thinking,
            Usage = usage,
            TokenUsageAvailable = usageElement is not null,
            StartedAtUtc = Date(measurement, "startedAt", "createdAt"),
            ExecutedAtUtc = executedAt,
            DurationMs = duration,
            CostEstimate = usageElement is null ? null : cost.Total,
            CostStatus = usageElement is null ? PriceStatus.UsageUnavailable : cost.Status,
            RawOutcome = rawOutcome ?? lane,
            RawReviewOutcome = rawReviewOutcome,
            Grade = grade,
            SemanticReissue = explicitSemanticReissue,
            ReissueReason = reissueReason,
            ObservedAtUtc = observedAt,
        };
        var classification = new AgentStudioOutcomeClassification
        {
            ObservationId = observationId,
            DecisionId = decisionId,
            Category = category,
            ReviewOutcome = reviewOutcome,
            ReissueReason = reissueReason,
        };
        return new AgentStudioRunRecord
        {
            TaskKey = taskKey, Run = run, RoutingDecision = decisionRecord, OutcomeObservation = observation,
            OutcomeClassification = classification, Project = Text(task, "project", "projectId"),
            Provider = listing?.Vendor ?? ProviderFromCli(Text(measurement, "actualCliType", "cliType")
                ?? Text(route, "cliType") ?? Text(task, "cliType")),
            Model = cost.ModelId ?? model, ThinkingLevel = thinking, RouteGranularity = granularity,
            CliType = Text(measurement, "actualCliType", "cliType") ?? Text(route, "cliType")
                ?? Text(task, "cliType"), TaskType = Text(task, "taskType"),
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
            CostCaveat = usageElement is null ? null : cost.Caveat,
            CostUnconfirmed = usageElement is not null && cost.Unconfirmed, Outcome = QualityOutcome(category, lane),
            Grade = grade,
            SemanticReissue = explicitSemanticReissue ?? (category != AgentStudioAttemptOutcomeCategory.Unknown
                ? classification.IsSemanticFailure : null),
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
                CostEstimate = null, Currency = null, CostCaveat = null, CostUnconfirmed = false, CostStatus = PriceStatus.UnknownModel,
                RoutingDecision = newest.RoutingDecision is { } decision
                    ? decision with { SelectedModel = null, SelectedThinkingLevel = null } : null,
                OutcomeObservation = newest.OutcomeObservation is { } observation
                    ? observation with { ActualModel = null, ActualThinkingLevel = null, CostEstimate = null, CostStatus = PriceStatus.UnknownModel } : null }
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
    private static int? NullableNumber(JsonElement? value, params string[] names)
        => value is { } item ? NullableNumber(item, names) : null;
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
    private static DateTime? Date(JsonElement? value, params string[] names)
        => value is { } item ? Date(item, names) : null;
    private static bool? Boolean(JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (Property(value, name) is { ValueKind: JsonValueKind.True }) return true;
            if (Property(value, name) is { ValueKind: JsonValueKind.False }) return false;
        }
        return null;
    }
    private static bool? Boolean(JsonElement? value, params string[] names)
        => value is { } item ? Boolean(item, names) : null;
    private static T? EnumValue<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(Normalize(value), true, out var parsed) ? parsed : null;
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
    private static AgentStudioReviewOutcome ReviewOutcome(string? value, string? grade)
    {
        if (grade is not null)
            return grade switch
            {
                "A" => AgentStudioReviewOutcome.GradeA,
                "B" => AgentStudioReviewOutcome.GradeB,
                "C" => AgentStudioReviewOutcome.GradeC,
                "D" => AgentStudioReviewOutcome.GradeD,
                _ => AgentStudioReviewOutcome.Unknown,
            };
        return Normalize(value) switch
        {
            "approved" or "accepted" or "pass" or "passed" => AgentStudioReviewOutcome.Approved,
            "changesrequested" or "needschanges" or "reissue" => AgentStudioReviewOutcome.ChangesRequested,
            "rejected" or "failed" => AgentStudioReviewOutcome.Rejected,
            _ => AgentStudioReviewOutcome.Unknown,
        };
    }
    private static AgentStudioAttemptOutcomeCategory OutcomeCategory(
        string? rawOutcome,
        string? reissueReason,
        string? grade,
        bool? semanticReissue,
        string? lane)
    {
        foreach (var value in new[] { reissueReason, rawOutcome, lane })
        {
            var normalized = Normalize(value);
            var category = normalized switch
            {
                "semantic" or "semanticfailure" or "implementationfailure" or "incorrectresult" => AgentStudioAttemptOutcomeCategory.SemanticFailure,
                "substantivereview" or "substantivelowgrade" or "substantivecordreview" => AgentStudioAttemptOutcomeCategory.SubstantiveReview,
                "environmental" or "environmentalfailure" or "infrastructurefailure" or "infrafailure" => AgentStudioAttemptOutcomeCategory.EnvironmentalFailure,
                "stalebase" or "stalebranch" => AgentStudioAttemptOutcomeCategory.StaleBase,
                "brokentesthost" or "testhostfailure" or "brokengate" => AgentStudioAttemptOutcomeCategory.BrokenTestHost,
                "cancellation" or "cancelled" or "canceled" => AgentStudioAttemptOutcomeCategory.Cancellation,
                "quotatruncation" or "quotatruncated" or "quotaexhausted" or "tokenlimit" => AgentStudioAttemptOutcomeCategory.QuotaTruncation,
                "missingdeliverypath" or "deliveryfailure" or "missingterminalsentinel" => AgentStudioAttemptOutcomeCategory.MissingDeliveryPath,
                "successful" or "success" or "completed" or "done" or "merged" => AgentStudioAttemptOutcomeCategory.Successful,
                _ => AgentStudioAttemptOutcomeCategory.Unknown,
            };
            if (category != AgentStudioAttemptOutcomeCategory.Unknown) return category;
        }
        if (grade is "C" or "D") return AgentStudioAttemptOutcomeCategory.SubstantiveReview;
        if (semanticReissue == true) return AgentStudioAttemptOutcomeCategory.SemanticFailure;
        return Outcome(lane) switch
        {
            OutcomeQualitySignal.Successful => AgentStudioAttemptOutcomeCategory.Successful,
            _ => AgentStudioAttemptOutcomeCategory.Unknown,
        };
    }
    private static string Normalize(string? value) => value is null
        ? ""
        : new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string StableId(string value) => "agent-studio-observation-"
        + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static OutcomeQualitySignal QualityOutcome(AgentStudioAttemptOutcomeCategory category, string? lane)
        => category switch
        {
            AgentStudioAttemptOutcomeCategory.Successful => OutcomeQualitySignal.Successful,
            AgentStudioAttemptOutcomeCategory.SubstantiveReview => OutcomeQualitySignal.NeedsReview,
            AgentStudioAttemptOutcomeCategory.Unknown => Outcome(lane),
            _ => OutcomeQualitySignal.Unsuccessful,
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
