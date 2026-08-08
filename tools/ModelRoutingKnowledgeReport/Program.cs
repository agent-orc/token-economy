using System.Text;
using TokenEconomy;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ModelRoutingKnowledgeReport -- <output.md> [review-evidence.json]");
    return 2;
}

var outputPath = Path.GetFullPath(args[0]);
var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var reviewEvidencePath = args.Length == 2
    ? Path.GetFullPath(args[1], repositoryRoot)
    : Path.Combine(repositoryRoot, "results", "routing-evidence", "review", "v1", "review-evidence.json");
var knowledge = ModelRoutingKnowledgeBase.PolicyOnly.WithReviewEvidence(
    ReviewEvidencePipeline.LoadReport(reviewEvidencePath));
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, ModelRoutingKnowledgeRenderer.Render(knowledge), new UTF8Encoding(false));
Console.WriteLine(outputPath);
return 0;

static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
    throw new DirectoryNotFoundException("Could not find TokenEconomy.slnx above the current directory.");
}
