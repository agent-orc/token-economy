using System.Text.Json;
using TokenEconomy;

var catalog = BenchmarkEvidenceCatalog.Default;
var model = KnownModels.ClaudeFable51;
var asOf = new DateOnly(2026, 9, 12);
var found = false;

foreach (var type in catalog.Types
    .Where(type => type.CapabilityClass == BenchmarkCapabilityClass.CodeReview)
    .OrderBy(type => type.Id, StringComparer.Ordinal))
{
    foreach (var result in catalog.ResultsFor(type.Id)
        .Where(result => result.ModelId == model && result.PublishedAt <= asOf)
        .OrderBy(result => result.PublishedAt))
    {
        found = true;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Model = result.ModelId.Value,
            Effort = result.ReasoningEffort.ToString(),
            Metric = type.Name,
            BenchmarkTypeId = type.Id,
            type.Version,
            result.Score,
            type.Unit,
            OriginalScale = new { type.MinimumScore, type.MaximumScore,
                Direction = type.Direction.ToString() },
            type.CitationNote,
            result.PublishedAt,
            result.RetrievedAt,
            result.SourceUrl,
            type.MethodologyUrl,
            result.EvidenceExcerpt,
            result.Context
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
if (!found) Console.WriteLine("No published review evidence for this model at this date.");
// Each object is one original metric and protocol. No blended quality score.
// Unspecified means the publisher did not identify the API reasoning effort.

