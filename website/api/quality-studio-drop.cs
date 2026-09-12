using System.Text.Json;
using System.Text.Json.Serialization;
using TokenEconomy;

if (args.Length != 1)
    throw new ArgumentException("Expected a directory of Token Economy schema-v1 review-run JSON files.");

var importer = new QualityStudioReviewRunImporter();
var runs = Directory.EnumerateFiles(args[0], "*.json", SearchOption.TopDirectoryOnly)
    .Order(StringComparer.Ordinal)
    .Select(path => importer.Import(path, $"quality-studio-drop/{Path.GetFileName(path)}"))
    .ToArray();

foreach (var group in runs.GroupBy(run => run.SourceRunId, StringComparer.Ordinal))
    if (group.Select(run => run.SourceArtifactSha256).Distinct().Skip(1).Any())
        throw new InvalidDataException($"Conflicting content for review run {group.Key}.");

var report = new ReviewEvidenceAggregator().Aggregate(runs);
var knowledge = ModelRoutingKnowledgeBase.PolicyOnly.WithReviewEvidence(report);
var models = runs.Select(run => run.CanonicalModel).OfType<string>().Distinct();
var quality = models.Select(model => knowledge.ReviewQualityFor(model)).ToArray();

Console.WriteLine(JsonSerializer.Serialize(new
{
    report.ImportedRunCount,
    report.FixtureRunCount,
    report.EligibleOperationalRunCount,
    report.ConfidenceGates,
    IneligibleRuns = runs.Where(run => !run.EligibleForAggregation)
        .Select(run => new { run.SourceRunId, run.EligibilityIssues }),
    ReviewQuality = quality
}, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));
// Read-only: no review launch, evidence write, task mutation or provider call.
// Native QS /review/runs/{id}/report JSON is a different contract; do not rename it.
// Unknown assessments stay null. Fixtures cannot supply operational evidence.

