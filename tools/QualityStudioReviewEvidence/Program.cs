using System.Text.Json;
using System.Text.Json.Serialization;
using TokenEconomy;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/QualityStudioReviewEvidence -- <quality-studio-drop-path> [output-root]");
    return 2;
}

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var dropPath = Path.GetFullPath(args[0], repositoryRoot);
var outputRoot = args.Length == 2
    ? Path.GetFullPath(args[1], repositoryRoot)
    : Path.Combine(repositoryRoot, "results", "routing-evidence", "review");
var report = new ReviewEvidencePipeline().Run(repositoryRoot, dropPath, outputRoot);

if (Environment.GetEnvironmentVariable("JOB_RESULTS_DIR") is { Length: > 0 } jobResultsDirectory)
    ReviewEvidencePipeline.WriteDerived(Path.Combine(jobResultsDirectory, "review-evidence.json"), report);

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
}));
return 0;

static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
    throw new DirectoryNotFoundException("Could not find TokenEconomy.slnx above the current directory.");
}
