using System.Net.Http.Json;
using System.Text.Json;

if (args.Length != 3)
    throw new ArgumentException("Expected Quality Studio base URL, repository ID and review run ID.");

using var client = new HttpClient { BaseAddress = new Uri(args[0]) };
var endpoint = $"/api/repos/{Uri.EscapeDataString(args[1])}/review/runs/"
    + $"{Uri.EscapeDataString(args[2])}/report?format=json";
var report = await client.GetFromJsonAsync<JsonElement>(endpoint);
var run = report.GetProperty("run");
var observations = report.GetProperty("observations");
var knownFindings = 0;
var missingFindingCollections = 0;
foreach (var observation in observations.EnumerateArray())
{
    if (observation.TryGetProperty("findings", out var findings)
        && findings.ValueKind == JsonValueKind.Array)
        knownFindings += findings.GetArrayLength();
    else
        missingFindingCollections++;
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    sourceEndpoint = endpoint,
    runId = run.GetProperty("id"),
    revision = run.GetProperty("revision"),
    observationCount = observations.GetArrayLength(),
    // Counts entries, including repeated/reused findings; no unique-defect or verification claim.
    findingEntryCount = missingFindingCollections == 0 ? knownFindings : (int?)null,
    missingFindingCollections,
    originalReport = report
}, new JsonSerializerOptions { WriteIndented = true }));
// This is native QS JSON, not a Token Economy schema-v1 review-run drop.
// Preserve native finding states and optional evidence/reviewer fields unchanged.
// run.model/thinkingLevel are requested settings; they do not prove the actual route.
// The reader performs one GET and does not launch, assess or mutate a review.
