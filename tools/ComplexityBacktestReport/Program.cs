using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TokenEconomy;

const int sampleSize = 30;
var apiBase = args.FirstOrDefault() ?? "http://127.0.0.1:5031";
var outputDirectory = Path.GetFullPath(args.Skip(1).FirstOrDefault()
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "results", "complexity-backtest"));

using var client = new HttpClient { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromSeconds(30) };
var summaries = await client.GetFromJsonAsync<List<TaskSummary>>("/api/tasks")
    ?? throw new InvalidDataException("The task API returned no task list.");
var archive = await client.GetFromJsonAsync<ArchiveResponse>("/api/tasks/archive?offset=0&limit=200")
    ?? throw new InvalidDataException("The task API returned no archive.");
var candidates = summaries.Concat(archive.Items)
    .Where(task => !task.Fixture)
    .GroupBy(task => task.Key, StringComparer.Ordinal)
    .Select(group => group.OrderByDescending(task => task.LastActivity).First())
    .OrderByDescending(task => task.LastActivity)
    .ThenBy(task => task.Key, StringComparer.Ordinal)
    .ToArray();

var observations = new List<Observation>(sampleSize);
var scanned = 0;
foreach (var summary in candidates)
{
    if (observations.Count == sampleSize) break;
    scanned++;
    var detail = await client.GetFromJsonAsync<TaskDetail>($"/api/tasks/{Uri.EscapeDataString(summary.Key)}")
        ?? throw new InvalidDataException($"No detail returned for {summary.Key}.");
    var tokenSummary = detail.Info.TokenSummary ?? summary.TokenSummary;
    if (string.IsNullOrWhiteSpace(detail.PromptMarkdown) || tokenSummary?.Entries.Length is not > 0) continue;

    var measuredEntries = MeasuredEntries(tokenSummary).OrderBy(entry => entry.Timestamp).ToArray();
    var first = measuredEntries[0].Timestamp;
    var last = measuredEntries[^1].Timestamp;
    var prompt = detail.PromptMarkdown;
    var referencedFiles = FileReferenceRegex().Matches(prompt).Select(match => match.Value.Trim('`')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var subsystems = referencedFiles
        .Select(path => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    observations.Add(new Observation(
        new ComplexityHistorySample
        {
            Card = new ComplexityCard
            {
                TaskKey = summary.Key,
                Prompt = prompt,
                Project = summary.ProjectName,
                TaskType = summary.TaskType,
                AcceptanceCriteria = BulletRegex().Matches(prompt).Select(match => match.Groups[1].Value.Trim()).ToArray(),
                ReferencedFiles = referencedFiles,
                ReferencedSubsystems = subsystems,
            },
            ActualTokens = measuredEntries.Sum(TotalTokens),
            ActualDuration = last - first,
            ReissueCount = Math.Max(0, measuredEntries.Length - 1),
        },
        summary.Title,
        summary.State,
        first,
        last,
        measuredEntries.Length));
}
if (observations.Count < sampleSize)
    throw new InvalidDataException($"Only {observations.Count} prompt-and-metrics cards were found after scanning {scanned} live and archived candidates; {sampleSize} are required.");

var samples = observations.Select(value => value.Sample).ToArray();
var aggregate = ComplexityBacktester.Run(samples);
var estimator = new TaskComplexityEstimator();
var rows = observations.Select((observation, index) =>
{
    var estimate = estimator.Estimate(observation.Sample.Card, samples.Where((_, other) => other != index));
    return new ReportRow(
        observation.Sample.Card.TaskKey,
        observation.Title,
        observation.Sample.Card.Project,
        observation.Sample.Card.TaskType,
        observation.State,
        observation.Sample.ActualTokens,
        observation.Sample.ReissueCount,
        Math.Round(observation.Sample.ActualDuration.TotalHours, 3),
        ActualLevel(observation.Sample).ToString().ToLowerInvariant(),
        estimate.Level.ToString().ToLowerInvariant(),
        estimate.Score,
        estimate.Confidence,
        estimate.PredictedTokens,
        estimate.PredictedReissues,
        estimate.Neighbours.Select(neighbour => neighbour.TaskKey).ToArray());
}).ToArray();

var generatedAt = DateTimeOffset.UtcNow;
Directory.CreateDirectory(outputDirectory);
var jsonPath = Path.Combine(outputDirectory, "agent-studio-30-card-backtest.json");
var markdownPath = Path.Combine(outputDirectory, "agent-studio-30-card-backtest.md");
var payload = new
{
    schemaVersion = 1,
    generatedAtUtc = generatedAt,
    source = new { endpoint = "/api/tasks + /api/tasks/archive + /api/tasks/{key}", selection = "30 newest non-fixture live or archived cards with prompt text and at least one token entry, ordered by task last activity", candidatesScanned = scanned },
    measurement = new
    {
        tokens = "sum of input, output, cache-read, and cache-creation tokens in agent:* entries; legacy cards without agent attribution use all entries",
        reissues = "measured entry count minus one",
        duration = "wall-clock span from first through last measured token entry; zero for single-entry cards",
    },
    aggregate,
    rows,
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(generatedAt, scanned, aggregate, rows));
Console.WriteLine($"Wrote {markdownPath}");
Console.WriteLine($"Wrote {jsonPath}");

static bool IsAgentEntry(TokenEntry entry) => entry.ParticipantId.StartsWith("agent:", StringComparison.OrdinalIgnoreCase);
static TokenEntry[] MeasuredEntries(TokenSummary summary)
{
    var agentEntries = summary.Entries.Where(IsAgentEntry).ToArray();
    return agentEntries.Length > 0 ? agentEntries : summary.Entries;
}
static long TotalTokens(TokenEntry entry) => entry.InputTokens + entry.OutputTokens + entry.CacheReadTokens + entry.CacheCreationTokens;
static TaskComplexityLevel ActualLevel(ComplexityHistorySample sample)
{
    var score = Math.Min(100, Math.Max(0, Math.Log10(Math.Max(1, sample.ActualTokens) / 10_000d) * 28
        + Math.Log2(1 + Math.Max(0, sample.ActualDuration.TotalHours)) * 7 + sample.ReissueCount * 12));
    return score switch
    {
        <= 20 => TaskComplexityLevel.Trivial,
        <= 50 => TaskComplexityLevel.Standard,
        <= 69 => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };
}

static string BuildMarkdown(DateTimeOffset generatedAt, int candidatesScanned, ComplexityBacktestResult result, ReportRow[] rows)
{
    var text = new System.Text.StringBuilder();
    text.AppendLine("# Agent Studio 30-card complexity backtest");
    text.AppendLine();
    text.AppendLine($"Generated {generatedAt:yyyy-MM-dd HH:mm:ss} UTC from the live Agent Studio task API.");
    text.AppendLine();
    text.AppendLine("## Result");
    text.AppendLine();
    text.AppendLine("| Metric | Result |");
    text.AppendLine("|---|---:|");
    text.AppendLine($"| Cards | {result.SampleCount} |");
    text.AppendLine($"| Complexity-band accuracy ({result.BandEvaluationCount}/{result.SampleCount}) | {Format(result.LevelAccuracy, "P1")} |");
    text.AppendLine($"| Token median absolute percentage error ({result.TokenEvaluationCount}/{result.SampleCount}) | {Format(result.TokenMedianAbsolutePercentageError, "P1")} |");
    text.AppendLine($"| Reissue mean absolute error ({result.ReissueEvaluationCount}/{result.SampleCount}) | {Format(result.ReissueMeanAbsoluteError, "F3")} |");
    text.AppendLine($"| Token-cost Spearman rank correlation | {Format(result.TokenRankCorrelation, "F3")} |");
    text.AppendLine();
    text.AppendLine("This is an observational leave-one-card-out backtest of the deterministic-plus-historical estimator. Each estimate was produced with the other 29 cards only.");
    text.AppendLine();
    text.AppendLine("## Cohort and measurements");
    text.AppendLine();
    text.AppendLine($"The generator scanned {candidatesScanned} newest live and archived candidates to obtain 30 non-fixture cards with both prompt text and measured token entries. Cards are ordered by task last activity. Current lane is retained in the JSON audit rows but is not an eligibility condition: durable run metrics remain usable after a card is archived or moved between lanes.");
    text.AppendLine();
    text.AppendLine("- Actual tokens are the sum of input, output, cache-read, and cache-creation tokens for `agent:*` entries. For legacy cards without agent attribution, all token entries are used and this fallback is visible in the source data.");
    text.AppendLine("- Reissues are measured entry count minus one. This is a measurable attempt proxy, not a semantic classification of why a retry happened.");
    text.AppendLine("- Duration is the span between first and last measured token entries. Single-entry cards therefore have a zero-hour span; duration accuracy is not reported.");
    text.AppendLine("- Prompt bullet items and path-like strings are extracted as acceptance criteria and touched-surface hints. No post-run changed-file data is used as an input.");
    text.AppendLine("- Repository file count and dependency fan-out are unavailable from this API snapshot. The repository-size term is therefore absent; this cohort cannot validate whether it improves prediction.");
    text.AppendLine("- The cohort is recent rather than temporally held out, and historical routing is confounded with task difficulty. These figures are calibration evidence, not a causal model-comparison claim.");
    text.AppendLine();
    text.AppendLine("## Per-card evidence");
    text.AppendLine();
    text.AppendLine("| Card | Project | Type | Actual tokens | Reissues | Predicted tokens | Predicted reissues | Actual band | Estimated band | Confidence |");
    text.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|---:|");
    foreach (var row in rows)
        text.AppendLine($"| {Escape(row.TaskKey)} | {Escape(row.Project ?? "")} | {Escape(row.TaskType ?? "")} | {row.ActualTokens.ToString("N0", CultureInfo.InvariantCulture)} | {row.ActualReissues} | {row.PredictedTokens.ToString("N0", CultureInfo.InvariantCulture)} | {row.PredictedReissues.ToString("F2", CultureInfo.InvariantCulture)} | {row.ActualLevel} | {row.EstimatedLevel} | {row.Confidence.ToString("P1", CultureInfo.InvariantCulture)} |");
    text.AppendLine();
    text.AppendLine("The adjacent JSON artifact contains titles, state, duration proxy, score, and neighbour keys for audit and machine consumption.");
    return text.ToString();
}

static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
static string Format(double? value, string format) => value?.ToString(format, CultureInfo.InvariantCulture) ?? "unavailable";

sealed record Observation(ComplexityHistorySample Sample, string Title, string State, DateTimeOffset FirstEntry, DateTimeOffset LastEntry, int AgentEntryCount);
sealed record ReportRow(string TaskKey, string Title, string? Project, string? TaskType, string State, long ActualTokens, int ActualReissues, double ObservedDurationHours, string ActualLevel, string EstimatedLevel, double Score, double Confidence, long PredictedTokens, double PredictedReissues, string[] Neighbours);

sealed record TaskDetail(
    [property: JsonPropertyName("info")] TaskDetailInfo Info,
    [property: JsonPropertyName("promptMarkdown")] string PromptMarkdown);
sealed record TaskDetailInfo([property: JsonPropertyName("tokenSummary")] TokenSummary? TokenSummary);
sealed record ArchiveResponse([property: JsonPropertyName("items")] TaskSummary[] Items);
sealed record TaskSummary(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("projectName")] string? ProjectName,
    [property: JsonPropertyName("taskType")] string? TaskType,
    [property: JsonPropertyName("lastActivity")] DateTimeOffset LastActivity,
    [property: JsonPropertyName("fixture")] bool Fixture,
    [property: JsonPropertyName("tokenSummary")] TokenSummary? TokenSummary);
sealed record TokenSummary([property: JsonPropertyName("entries")] TokenEntry[] Entries);
sealed record TokenEntry(
    [property: JsonPropertyName("ts")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("participantId")] string ParticipantId,
    [property: JsonPropertyName("inputTokens")] long InputTokens,
    [property: JsonPropertyName("outputTokens")] long OutputTokens,
    [property: JsonPropertyName("cacheReadTokens")] long CacheReadTokens,
    [property: JsonPropertyName("cacheCreationTokens")] long CacheCreationTokens);

partial class Program
{
    [GeneratedRegex(@"(?m)^\s*[-*]\s+(.+)$")]
    internal static partial Regex BulletRegex();

    [GeneratedRegex(@"`?(?:[\w.-]+[/\\])+[\w.-]+\.[A-Za-z0-9]+`?")]
    internal static partial Regex FileReferenceRegex();
}
