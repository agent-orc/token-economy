using System.Text.Json;
using System.Text.Json.Serialization;
using TokenEconomy;

// dotnet run -- <exported-task-storage> <intake-snapshots.json> <cutoff-UTC>
// Reads an AGT export. It does not change task storage or call a model.
if (args.Length != 3)
    throw new ArgumentException("Expected task-storage path, intake snapshot JSON, and UTC cutoff.");

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
options.Converters.Add(new JsonStringEnumConverter());
var snapshots = JsonSerializer.Deserialize<FrozenIntake[]>(File.ReadAllText(args[1]), options)
    ?? throw new InvalidDataException("Intake snapshots are missing.");
var cutoff = DateTimeOffset.Parse(args[2]).UtcDateTime;
if (snapshots.Any(item => item.CapturedAtUtc == default || item.FirstLaunchAtUtc == default
    || item.CapturedAtUtc.Kind != DateTimeKind.Utc || item.FirstLaunchAtUtc.Kind != DateTimeKind.Utc
    || item.CapturedAtUtc > item.FirstLaunchAtUtc))
    throw new InvalidDataException("Intake timestamps must be present, UTC, and captured no later than the first launch.");
if (snapshots.GroupBy(item => item.Card.TaskKey, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
    throw new InvalidDataException("Keep one frozen first-launch intake per task key.");

var intakeByKey = snapshots.ToDictionary(item => item.Card.TaskKey, StringComparer.OrdinalIgnoreCase);
var store = new InMemoryAgentStudioRunStore();
new AgentStudioTaskStorageImporter().ImportDirectory(args[0], store);
var coverage = ComplexityBacktester.MeasureCoverage(store.Records);
Console.WriteLine(JsonSerializer.Serialize(coverage, options));

// Current card text is never accepted as a historical pre-launch snapshot.
var eligible = store.Records.Where(record => intakeByKey.TryGetValue(record.TaskKey, out var intake)
    && intake.CompletedAtUtc is not null && intake.CompleteAttemptHistory).ToArray();
var prepared = eligible.Select(record => record with
{
    TaskPrompt = intakeByKey[record.TaskKey].Card.Prompt
}).ToArray();
var samples = ComplexityHistory.FromRunRecords(prepared)
    .Select(sample => sample with
    {
        Card = intakeByKey[sample.Card.TaskKey].Card,
        // A transport retry or entry count is not a measured semantic reissue.
        ReissueHistoryAvailable = sample.SemanticReissueCount is not null,
        ReissueCount = sample.SemanticReissueCount ?? 0
    }).ToArray();

// No card that overlaps the cutoff enters either group. Completed training
// outcomes must have been observable before the evaluated work started.
var training = samples.Where(sample => intakeByKey[sample.Card.TaskKey].CompletedAtUtc <= cutoff
    && prepared.Where(record => record.TaskKey.Equals(sample.Card.TaskKey, StringComparison.OrdinalIgnoreCase))
        .All(record => record.ExecutedAtUtc <= cutoff && record.ObservedAtUtc <= cutoff)).ToArray();
var evaluation = samples.Where(sample => intakeByKey[sample.Card.TaskKey].FirstLaunchAtUtc >= cutoff).ToArray();
Console.WriteLine($"Imported {store.Records.Count} attempts; retained {training.Length} training and {evaluation.Length} held-out cards.");
if (training.Length == 0 || evaluation.Length == 0)
    throw new InvalidDataException("Both a completed training cohort and a later evaluation cohort are required.");

var report = ComplexityBacktester.RunHeldOut(training, evaluation);
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(options) { WriteIndented = true }));

var estimator = new TaskComplexityEstimator();
foreach (var sample in evaluation)
{
    var estimate = estimator.Estimate(sample.Card, training);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        sample.Card.TaskKey,
        estimate.Score,
        estimate.Level,
        estimate.Confidence,
        estimate.ScoreEvidence,
        estimate.HistoricalEvidence,
        estimate.TokenForecast,
        actualTokens = sample.TokenHistoryComplete ? (long?)sample.ActualTokens : null,
        actualDuration = sample.DurationHistoryComplete ? (TimeSpan?)sample.ActualDuration : null,
        semanticReissues = sample.SemanticReissueCount,
        estimate.Neighbours
    }, options));
}

// The collector owns these timestamps and the completeness check. They must
// come from retained intake/attempt records, never from a retrospective guess.
public sealed record FrozenIntake(
    ComplexityCard Card,
    DateTime CapturedAtUtc,
    DateTime FirstLaunchAtUtc,
    DateTime? CompletedAtUtc,
    bool CompleteAttemptHistory);
