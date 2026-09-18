using System.Text.Json;
using TokenEconomy;

// Run from the repository root. Writes only to the explicitly supplied report path.
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = true };
var data = JsonDocument.Parse(File.ReadAllText("src/TokenEconomy/catalog/context-cost-evidence.json")).RootElement;
var cases = new List<object>();
var findings = new List<object>();
foreach (var preset in data.GetProperty("presets").EnumerateArray())
{
    var parameters = preset.GetProperty("parameters");
    foreach (var policy in data.GetProperty("cachePolicies").EnumerateObject())
    {
        var tasks = parameters.GetProperty("tasks").GetInt32();
        var perTask = parameters.GetProperty("turnsPerTask").GetInt32();
        // Required members are injected before deserialization to keep one input schema.
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(parameters.GetRawText())!;
        fields["modelId"] = JsonSerializer.SerializeToElement(policy.Name);
        fields["startUtc"] = JsonSerializer.SerializeToElement("2026-09-18T00:00:00Z");
        var o = JsonSerializer.Deserialize<ContextSessionOptions>(JsonSerializer.Serialize(fields), json)! with
        {
            Turns = tasks * perTask,
            CacheTtlMinutes = policy.Value.GetProperty("ttlMinutes").GetDouble(),
            MinimumCacheTokens = policy.Value.GetProperty("minimumCacheTokens").GetInt64()
        };
        var longRun = ContextSessionCost.Forecast(o);
        var freshOptions = o with { InitialContext = o.RestartContext, RestartEveryTurns = perTask };
        var fresh = ContextSessionCost.Forecast(freshOptions);
        var compactOptions = o with { CompactionThreshold = (long)Math.Ceiling(Math.Max(o.InitialContext * 1.5, o.CompactedContext + 1)) };
        var compact = ContextSessionCost.Forecast(compactOptions);
        var cold = ContextSessionCost.Forecast(o with { GapMinutes = 60 });
        var idle = ContextSessionCost.Forecast(o with { GapsMinutes = Enumerable.Range(0, o.Turns)
            .Select(i => i == 0 ? 0d : i % perTask == 0 ? 10d : 1d).ToArray() });
        var firstCompaction = compact.Turns.FirstOrDefault(t => t.Compacted)?.Turn;
        var compactionPayback = firstCompaction is null ? null : compact.Turns
            .FirstOrDefault(t => t.Turn >= firstCompaction && t.CumulativeCost <= longRun.Turns[t.Turn - 1].CumulativeCost)?.Turn;
        foreach (var option in new[] { o, freshOptions, compactOptions, o with { GapMinutes = 60 } })
            cases.Add(new { options = option, forecast = ContextSessionCost.Forecast(option) });
        var extended = o with { Turns = 1000 * perTask };
        var crossing = ContextSessionCost.FirstMoreExpensiveTask(ContextSessionCost.Forecast(extended),
            ContextSessionCost.Forecast(extended with { InitialContext = o.RestartContext, RestartEveryTurns = perTask }), perTask);
        findings.Add(new { scenario = preset.GetProperty("id").GetString(), model = policy.Name, tasks, turns = o.Turns,
            longUsd = longRun.Total, freshUsd = fresh.Total, compactUsd = compact.Total, coldUsd = cold.Total, tenMinuteTaskGapUsd = idle.Total, firstCompactionTurn = firstCompaction, compactionPaybackTurn = compactionPayback,
            usdPerTask = longRun.Total / tasks, firstCostlierTaskWithin1000 = crossing,
            firstDecisionUsd = longRun.Turns[0].Cost.Total, lastDecisionUsd = longRun.Turns[^1].Cost.Total });
    }
}
var random = new Random(54);
for (var i = 0; i < 60; i++)
{
    var o = new ContextSessionOptions
    {
        ModelId = i % 2 == 0 ? "claude-opus-5" : "gpt-5.6-sol",
        StartUtc = new(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
        InitialContext = 10000, InitiallyCachedTokens = i % 3 == 0 ? 5000 : 0,
        ToolTokensPerTurn = random.Next(0, 5000), InputTokensPerTurn = random.Next(0, 100),
        OutputTokensPerTurn = random.Next(0, 1000), ReasoningTokensPerTurn = 200,
        RetainedReasoningTokensPerTurn = 100, Turns = 20, CacheTtlMinutes = i % 5 == 0 ? 0 : 5,
        GapMinutes = i % 7, MinimumCacheTokens = 1024, RestartEveryTurns = i % 4 == 0 ? 4 : 0,
        RestartContext = 5000, SharedPrefixTokens = i % 2 == 0 ? 500 : 3000,
        CompactionThreshold = i % 2 == 0 ? 15000 : 0, CompactedContext = 4000,
        CompactionOutputTokens = 300, CompactionMinutes = 2, CacheWritePerMTokOverride = i % 3 == 0 ? 10 : null
    };
    cases.Add(new { options = o, forecast = ContextSessionCost.Forecast(o) });
}
var output = args.Length > 0 ? args[0] : throw new ArgumentException("Supply an output JSON path.");
File.WriteAllText(output, JsonSerializer.Serialize(new { cases, findings }, json) + "\n");
Console.WriteLine($"Wrote {cases.Count} parity cases and {findings.Count} scenario/model findings to {output}");
