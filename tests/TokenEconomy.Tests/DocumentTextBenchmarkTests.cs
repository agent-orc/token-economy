using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class DocumentTextBenchmarkTests
{
    [Fact]
    public async Task Default_runner_invokes_every_canonical_catalog_model()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-text-benchmark-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "corpus"));
        await File.WriteAllTextAsync(Path.Combine(root, "corpus", "columns.pdf"), "fixture");
        try
        {
            var extractor = new FakeExtractor();
            var (result, _) = await new DocumentTextBenchmarkRunner(extractor).RunAsync(Corpus(), root, "all-models");
            var expected = ModelPriceCatalog.Default.Listings.Select(item => item.ModelId).ToArray();

            Assert.Equal(expected, result.Models);
            Assert.Equal(expected, extractor.Requests.Select(item => item.Model).ToArray());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Run_covers_every_catalog_model_and_builds_per_type_capabilities()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-text-benchmark-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "corpus"));
        await File.WriteAllTextAsync(Path.Combine(root, "corpus", "columns.pdf"), "fixture");
        try
        {
            var catalog = new ModelPriceCatalog(
            [
                new() { ModelId = "model-b" },
                new() { ModelId = "model-a" },
            ]);
            var extractor = new FakeExtractor();
            var runner = new DocumentTextBenchmarkRunner(extractor, catalog);
            var events = new List<string>();
            runner.EventOccurred += item => events.Add(item.Name);
            var corpus = Corpus();

            var (result, report) = await runner.RunAsync(corpus, root, "run-1");

            Assert.Equal(["model-b", "model-a"], result.Models);
            Assert.Equal(2, result.Cases.Count);
            Assert.Equal(2, extractor.Requests.Select(item => item.Model).Distinct().Count());
            Assert.All(extractor.Requests, item => Assert.True(Path.IsPathFullyQualified(item.DocumentPath)));
            Assert.Equal(2, report.Capabilities.Count);
            Assert.Equal(DocumentTextCapabilityLevel.Demonstrated, report.Capabilities.Single(item => item.Model == "model-a").Level);
            Assert.Equal(DocumentTextCapabilityLevel.NotDemonstrated, report.Capabilities.Single(item => item.Model == "model-b").Level);
            Assert.Equal(" LEFT   column\nRight column ", result.Cases.Single(item => item.Model == "model-a").ExtractedText);
            Assert.All(report.Capabilities, item => Assert.Equal("benchmarks/results/document-to-text/hard-cases/run-1.json", item.EvidenceReference));
            Assert.Contains("document_text_benchmark.run.started", events);
            Assert.Contains("document_text_benchmark.case.completed", events);
            Assert.Contains("document_text_benchmark.run.completed", events);
            Assert.True(File.Exists(Path.Combine(root, "benchmarks", "results", "document-to-text", "hard-cases", "run-1.json")));
            Assert.True(File.Exists(Path.Combine(root, "benchmarks", "results", "document-to-text", "hard-cases", "run-1.capabilities.json")));
            await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(corpus, root, "run-1"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Capability_report_is_partitioned_by_document_type_and_never_claims_unsupported()
    {
        var result = new DocumentTextBenchmarkResult
        {
            SchemaVersion = 1,
            CorpusId = "x",
            RunId = "r",
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            Models = ["m"],
            Cases =
            [
                Case(DocumentType.Pdf, "pdf-1", true),
                Case(DocumentType.Pdf, "pdf-2", false),
                Case(DocumentType.Word, "word-1", true),
            ],
        };

        var report = DocumentTextBenchmarkRunner.BuildCapabilityReport(result, "results/r.json");

        var pdf = Assert.Single(report.Capabilities, item => item.DocumentType == DocumentType.Pdf);
        Assert.Equal(DocumentTextCapabilityLevel.Partial, pdf.Level);
        Assert.Equal(0.5m, pdf.SuccessRate);
        var word = Assert.Single(report.Capabilities, item => item.DocumentType == DocumentType.Word);
        Assert.Equal(DocumentTextCapabilityLevel.Demonstrated, word.Level);
    }

    [Fact]
    public async Task Oracle_requires_fragments_in_order_and_rejects_forbidden_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-text-benchmark-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "corpus"));
        await File.WriteAllTextAsync(Path.Combine(root, "corpus", "columns.pdf"), "fixture");
        try
        {
            var catalog = new ModelPriceCatalog([new() { ModelId = "model-b" }]);
            var (result, _) = await new DocumentTextBenchmarkRunner(new FakeExtractor(), catalog)
                .RunAsync(Corpus(), root, "oracle");

            var failed = Assert.Single(result.Cases);
            Assert.False(failed.Succeeded);
            Assert.Contains("INTERNAL WATERMARK", failed.UnexpectedFragments);
            Assert.Contains("Right column", failed.MissingFragments);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Curated_corpus_covers_every_document_type_and_all_sources_exist()
    {
        var root = FindRepositoryRoot();
        var corpus = DocumentTextBenchmarkRunner.LoadCorpus(
            Path.Combine(root, "benchmarks", "document-to-text", "curated-hard-cases.json"));

        Assert.Equal(Enum.GetValues<DocumentType>(), corpus.Cases.Select(item => item.DocumentType).Distinct().Order().ToArray());
        Assert.All(corpus.Cases, item => Assert.True(File.Exists(Path.Combine(root, item.DocumentPath)), item.DocumentPath));
        Assert.All(corpus.Cases, item => Assert.NotEmpty(item.RequiredFragments));
        Assert.All(corpus.Cases, item => Assert.NotEmpty(item.ForbiddenFragments));
    }

    private static DocumentTextCorpus Corpus() => new()
    {
        SchemaVersion = 1,
        Id = "hard-cases",
        Cases =
        [
            new()
            {
                Id = "two-columns", DocumentType = DocumentType.Pdf,
                DocumentPath = "corpus/columns.pdf",
                RequiredFragments = ["Left column", "Right column"],
                ForbiddenFragments = ["INTERNAL WATERMARK"],
            },
        ],
    };

    private static DocumentTextCaseResult Case(DocumentType type, string id, bool succeeded) => new()
    {
        Model = "m",
        CaseId = id,
        DocumentType = type,
        Succeeded = succeeded,
        ExitCode = 0,
        Usage = default,
        DurationMs = 1,
    };

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Test repository root was not found.");
    }

    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public List<DocumentTextExtractionRequest> Requests { get; } = [];

        public Task<DocumentTextExtractionResponse> ExtractAsync(
            DocumentTextExtractionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var text = request.Model == "model-a"
                ? " LEFT   column\nRight column "
                : "Right column; Left column; INTERNAL WATERMARK";
            return Task.FromResult(new DocumentTextExtractionResponse
            {
                ExitCode = 0,
                Usage = new(10, 5),
                ExtractedText = text,
            });
        }
    }
}
