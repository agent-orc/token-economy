using System.Text.Json;

namespace TokenEconomy;

/// <summary>Adapter for Agent Studio's start/end QuotaSnapshotEvent payloads (quota-snapshot observations).</summary>
public static class AgentStudioQuotaEvidence
{
    /// <summary>
    /// Use a host-verified account identity and exclusive run attribution; cached, shared or reset
    /// readings do not establish a per-card weekly cost. Raw payloads remain owned by the host.
    /// </summary>
    public static RunQuotaMeasurement? FromSnapshots(JsonElement start, JsonElement end,
        string subscriptionId, bool exclusiveAttribution)
    {
        if (!exclusiveAttribution || !Fresh(start) || !Fresh(end)
            || Text(start, "phase") != "start" || Text(end, "phase") != "end"
            || Text(start, "runId") is not { Length: > 0 } runId || Text(end, "runId") != runId
            || Text(start, "cliType") != Text(end, "cliType") || Text(start, "plan") != Text(end, "plan")
            || Date(start, "fetchedAt") is not { } before || Date(end, "fetchedAt") is not { } after || after <= before)
            return null;
        var a = Weekly(start); var b = Weekly(end);
        if (a is null || b is null || Text(a.Value, "label") != Text(b.Value, "label")
            || Date(a.Value, "resetAt") is not { } reset || Date(b.Value, "resetAt") != reset
            || Number(a.Value, "usedPct") is not { } usedBefore || Number(b.Value, "usedPct") is not { } usedAfter)
            return null;
        var result = new RunQuotaMeasurement(subscriptionId, Text(a.Value, "label")!, reset.AddDays(-7), reset,
            before, after, usedBefore, usedAfter, true);
        return result.Share is null ? null : result;
    }

    private static bool Fresh(JsonElement value) => !Flag(value, "missing") && !Flag(value, "stale")
        && !Flag(value, "suspicious") && string.IsNullOrEmpty(Text(value, "error"))
        && Number(value, "snapshotAgeSec") is >= 0 && Number(value, "ttlSeconds") is > 0
        && Number(value, "snapshotAgeSec") <= Number(value, "ttlSeconds");
    private static JsonElement? Weekly(JsonElement value)
    {
        if (!value.TryGetProperty("windows", out var windows) || windows.ValueKind != JsonValueKind.Array) return null;
        var matches = windows.EnumerateArray().Where(w => string.Equals(Text(w, "label"), "Weekly", StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    private static bool Flag(JsonElement value, string key) => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.True;
    private static string? Text(JsonElement value, string key) => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    private static decimal? Number(JsonElement value, string key) => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetDecimal(out var number) ? number : null;
    private static DateTime? Date(JsonElement value, string key) => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String && field.TryGetDateTime(out var date) ? date.ToUniversalTime() : null;
}
