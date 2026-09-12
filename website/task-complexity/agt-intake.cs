using System.Net.Http.Json;
using System.Text.Json;
using TokenEconomy;

// Run at admission, before the first routing decision.
// dotnet run -- <AGT-base-URL> <task-key> <new-snapshot-file.json>
if (args.Length != 3)
    throw new ArgumentException("Expected AGT base URL, task key, and a new snapshot file.");
using var client = new HttpClient { BaseAddress = new Uri(args[0]) };
var endpoint = $"/api/tasks/{Uri.EscapeDataString(args[1])}";
var detail = await client.GetFromJsonAsync<JsonElement>(endpoint);
var prompt = detail.GetProperty("promptMarkdown").GetString();
if (string.IsNullOrWhiteSpace(prompt))
    throw new InvalidDataException("The AGT card has no authored prompt.");
var info = detail.GetProperty("info");
string? Text(string name) => info.TryGetProperty(name, out var field)
    && field.ValueKind == JsonValueKind.String ? field.GetString() : null;

var card = new ComplexityCard
{
    TaskKey = args[1],
    Prompt = prompt,
    Project = Text("projectName"),
    TaskType = Text("taskType")
    // Add authored acceptance criteria, expected files/subsystems and reviewed
    // routing scores from your intake worksheet before calling Estimate.
    // Unknown fields remain unset; the API does not inspect the repository.
};
var capturedAtUtc = DateTime.UtcNow;
var estimate = new TaskComplexityEstimator().Estimate(card);
await using var output = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write);
await JsonSerializer.SerializeAsync(output, new
{
    capturedAtUtc,
    sourceEndpoint = endpoint,
    card,
    estimate
}, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"Saved {card.TaskKey}: {estimate.Score}/100, {estimate.Level}.");
Console.WriteLine(estimate.ConfidenceEvidence);
// This captures today's intake only. Historical replays need a retained old
// snapshot or verified dispatch evidence, as shown in the history guide.
