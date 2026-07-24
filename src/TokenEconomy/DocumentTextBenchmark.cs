using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>The document families measured by the curated document-to-text benchmark.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentType>))]
public enum DocumentType
{
    Pdf,
    Word,
    Spreadsheet,
    Presentation,
}

/// <summary>A versioned corpus of difficult, deterministic document extraction cases.</summary>
public sealed record DocumentTextCorpus
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public int InvocationTimeoutSeconds { get; init; } = 300;
    public required IReadOnlyList<DocumentTextCorpusCase> Cases { get; init; }
}

/// <summary>
/// One source document and its extraction oracle. Required fragments must occur in order; forbidden
/// fragments catch content that should not leak into the result, such as speaker notes or hidden cells.
/// </summary>
public sealed record DocumentTextCorpusCase
{
    public required string Id { get; init; }
    public required DocumentType DocumentType { get; init; }
    public required string DocumentPath { get; init; }
    public required IReadOnlyList<string> RequiredFragments { get; init; }
    public IReadOnlyList<string> ForbiddenFragments { get; init; } = [];
    public string? Note { get; init; }
}

/// <summary>The request passed to a model-backed document extractor.</summary>
public sealed record DocumentTextExtractionRequest(
    string CorpusId, string CaseId, DocumentType DocumentType, string Model, string DocumentPath);

/// <summary>The transport result returned by a model-backed document extractor.</summary>
public sealed record DocumentTextExtractionResponse
{
    public required int ExitCode { get; init; }
    public required TokenUsage Usage { get; init; }
    public string? ExtractedText { get; init; }
    public decimal? CostUsd { get; init; }
    public string? Error { get; init; }
}

/// <summary>Transport seam for local CLI, hosted API, and deterministic test extractors.</summary>
public interface IDocumentTextExtractor
{
    Task<DocumentTextExtractionResponse> ExtractAsync(
        DocumentTextExtractionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Raw, sufficient evidence for a single model/document attempt.</summary>
public sealed record DocumentTextCaseResult
{
    public required string Model { get; init; }
    public required string CaseId { get; init; }
    public required DocumentType DocumentType { get; init; }
    public required bool Succeeded { get; init; }
    public required int ExitCode { get; init; }
    public required TokenUsage Usage { get; init; }
    public string? ExtractedText { get; init; }
    public decimal? CostUsd { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<string> MissingFragments { get; init; } = [];
    public IReadOnlyList<string> UnexpectedFragments { get; init; } = [];
    public string? FailureReason { get; init; }
}

/// <summary>Append-only raw result from an all-model corpus run.</summary>
public sealed record DocumentTextBenchmarkResult
{
    public required int SchemaVersion { get; init; }
    public required string CorpusId { get; init; }
    public required string RunId { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required IReadOnlyList<string> Models { get; init; }
    public required IReadOnlyList<DocumentTextCaseResult> Cases { get; init; }
}

/// <summary>
/// Evidence-derived capability for one canonical model and document type. A record with no successes
/// explicitly says <see cref="DocumentTextCapabilityLevel.NotDemonstrated"/> rather than "unsupported":
/// one controlled corpus cannot prove universal lack of support.
/// </summary>
public sealed record DocumentTextCapabilityRecord
{
    public required string Model { get; init; }
    public required DocumentType DocumentType { get; init; }
    public required DocumentTextCapabilityLevel Level { get; init; }
    public required int CasesAttempted { get; init; }
    public required int CasesPassed { get; init; }
    public required decimal SuccessRate { get; init; }
    public required string EvidenceReference { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<DocumentTextCapabilityLevel>))]
public enum DocumentTextCapabilityLevel
{
    NotDemonstrated,
    Partial,
    Demonstrated,
}

/// <summary>Per-document-type capability artifact derived only from the adjacent raw run.</summary>
public sealed record DocumentTextCapabilityReport
{
    public required int SchemaVersion { get; init; }
    public required string CorpusId { get; init; }
    public required string RunId { get; init; }
    public required IReadOnlyList<DocumentTextCapabilityRecord> Capabilities { get; init; }
}

/// <summary>
/// Runs every canonical catalog model over every curated case, preserving raw evidence and a
/// deterministic per-model/per-document-type capability view.
/// </summary>
public sealed class DocumentTextBenchmarkRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDocumentTextExtractor _extractor;
    private readonly ModelPriceCatalog _models;

    public DocumentTextBenchmarkRunner(IDocumentTextExtractor extractor, ModelPriceCatalog? models = null)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _models = models ?? ModelPriceCatalog.Default;
    }

    public event Action<BenchmarkRunEvent>? EventOccurred;

    public static DocumentTextCorpus LoadCorpus(string path) =>
        JsonSerializer.Deserialize<DocumentTextCorpus>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"Could not deserialize document corpus: {path}");

    public async Task<(DocumentTextBenchmarkResult Result, DocumentTextCapabilityReport Capabilities)> RunAsync(
        DocumentTextCorpus corpus,
        string repositoryRoot,
        string? runId = null,
        CancellationToken cancellationToken = default)
    {
        Validate(corpus);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var started = DateTime.UtcNow;
        runId ??= started.ToString("yyyyMMddTHHmmssfffZ");
        var resultDirectory = Path.Combine(repositoryRoot, "benchmarks", "results", "document-to-text", corpus.Id);
        Directory.CreateDirectory(resultDirectory);
        var rawPath = Path.Combine(resultDirectory, runId + ".json");
        var capabilityPath = Path.Combine(resultDirectory, runId + ".capabilities.json");
        if (File.Exists(rawPath) || File.Exists(capabilityPath))
            throw new IOException($"Document benchmark run '{runId}' already exists; results are append-only.");

        var cases = corpus.Cases.Select(item => (
            Definition: item,
            Path: ResolveDocument(repositoryRoot, item.DocumentPath))).ToArray();
            var models = _models.Listings.Select(model => model.ModelId).ToArray();
            var results = new List<DocumentTextCaseResult>(models.Length * cases.Length);
            EventOccurred?.Invoke(new("document_text_benchmark.run.started", new Dictionary<string, object?>
            {
                ["corpusId"] = corpus.Id,
                ["runId"] = runId,
                ["models"] = models.Length,
                ["cases"] = cases.Length,
            }));

            foreach (var model in models)
                foreach (var item in cases)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var timer = Stopwatch.StartNew();
                    DocumentTextExtractionResponse response;
                    using (var invocationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        invocationTimeout.CancelAfter(TimeSpan.FromSeconds(corpus.InvocationTimeoutSeconds));
                        try
                        {
                            response = await _extractor.ExtractAsync(
                                new(corpus.Id, item.Definition.Id, item.Definition.DocumentType, model, item.Path),
                                invocationTimeout.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            response = new()
                            {
                                ExitCode = -1,
                                Usage = default,
                                Error = $"Extraction timed out after {corpus.InvocationTimeoutSeconds} seconds.",
                            };
                        }
                        catch (Exception error) when (error is not OperationCanceledException)
                        {
                            response = new() { ExitCode = -1, Usage = default, Error = error.Message };
                        }
                    }

                    var grade = Grade(response.ExtractedText, item.Definition);
                    var failure = response.ExitCode != 0
                        ? response.Error ?? $"Extractor exited {response.ExitCode}."
                        : grade.FailureReason;
                    timer.Stop();
                    var result = new DocumentTextCaseResult
                    {
                        Model = model,
                        CaseId = item.Definition.Id,
                        DocumentType = item.Definition.DocumentType,
                        Succeeded = failure is null,
                        ExitCode = response.ExitCode,
                        Usage = response.Usage,
                        ExtractedText = response.ExtractedText,
                        CostUsd = response.CostUsd,
                        DurationMs = timer.ElapsedMilliseconds,
                        MissingFragments = grade.Missing,
                        UnexpectedFragments = grade.Unexpected,
                        FailureReason = failure,
                    };
                    results.Add(result);
                    EventOccurred?.Invoke(new("document_text_benchmark.case.completed", new Dictionary<string, object?>
                    {
                        ["corpusId"] = corpus.Id,
                        ["runId"] = runId,
                        ["model"] = model,
                        ["caseId"] = item.Definition.Id,
                        ["documentType"] = item.Definition.DocumentType.ToString(),
                        ["succeeded"] = result.Succeeded,
                        ["durationMs"] = result.DurationMs,
                        ["failureReason"] = failure,
                    }));
                }

            var raw = new DocumentTextBenchmarkResult
            {
                SchemaVersion = 1,
                CorpusId = corpus.Id,
                RunId = runId,
                StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow,
                Models = models,
                Cases = results,
            };
            var evidenceReference = Path.GetRelativePath(repositoryRoot, rawPath).Replace('\\', '/');
            var capabilities = BuildCapabilityReport(raw, evidenceReference);
            await WriteNewAsync(rawPath, raw, cancellationToken);
            await WriteNewAsync(capabilityPath, capabilities, cancellationToken);
            EventOccurred?.Invoke(new("document_text_benchmark.run.completed", new Dictionary<string, object?>
            {
                ["corpusId"] = corpus.Id,
                ["runId"] = runId,
                ["attempts"] = results.Count,
                ["capabilityRecords"] = capabilities.Capabilities.Count,
        }));
        return (raw, capabilities);
    }

    public static DocumentTextCapabilityReport BuildCapabilityReport(
        DocumentTextBenchmarkResult result, string evidenceReference)
    {
        if (string.IsNullOrWhiteSpace(evidenceReference))
            throw new ArgumentException("An evidence reference is required.", nameof(evidenceReference));

        var records = result.Cases
            .GroupBy(item => (item.Model, item.DocumentType))
            .Select(group =>
            {
                var passed = group.Count(item => item.Succeeded);
                var attempted = group.Count();
                return new DocumentTextCapabilityRecord
                {
                    Model = group.Key.Model,
                    DocumentType = group.Key.DocumentType,
                    Level = passed == 0 ? DocumentTextCapabilityLevel.NotDemonstrated
                        : passed == attempted ? DocumentTextCapabilityLevel.Demonstrated
                        : DocumentTextCapabilityLevel.Partial,
                    CasesAttempted = attempted,
                    CasesPassed = passed,
                    SuccessRate = (decimal)passed / attempted,
                    EvidenceReference = evidenceReference,
                };
            })
            .OrderBy(item => item.Model, StringComparer.Ordinal)
            .ThenBy(item => item.DocumentType)
            .ToArray();

        return new()
        {
            SchemaVersion = 1,
            CorpusId = result.CorpusId,
            RunId = result.RunId,
            Capabilities = records,
        };
    }

    private static (IReadOnlyList<string> Missing, IReadOnlyList<string> Unexpected, string? FailureReason) Grade(
        string? extracted, DocumentTextCorpusCase definition)
    {
        var normalized = Normalize(extracted);
        var missing = new List<string>();
        var next = 0;
        foreach (var fragment in definition.RequiredFragments)
        {
            var wanted = Normalize(fragment);
            var found = normalized.IndexOf(wanted, next, StringComparison.Ordinal);
            if (found < 0) missing.Add(fragment);
            else next = found + wanted.Length;
        }
        var unexpected = definition.ForbiddenFragments
            .Where(fragment => normalized.Contains(Normalize(fragment), StringComparison.Ordinal))
            .ToArray();
        var failure = missing.Count > 0 || unexpected.Length > 0
            ? $"Extraction oracle failed: {missing.Count} required fragment(s) missing, {unexpected.Length} forbidden fragment(s) present."
            : null;
        return (missing, unexpected, failure);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var output = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(character)) { pendingSpace = output.Length > 0; continue; }
            if (pendingSpace) output.Append(' ');
            output.Append(char.ToLowerInvariant(character));
            pendingSpace = false;
        }
        return output.ToString();
    }

    private static void Validate(DocumentTextCorpus corpus)
    {
        if (corpus.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported document corpus schema version {corpus.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(corpus.Id) || corpus.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Corpus id must be a valid file name.");
        if (corpus.InvocationTimeoutSeconds < 1)
            throw new InvalidDataException("Document extraction timeout must be at least one second.");
        if (corpus.Cases.Count == 0) throw new InvalidDataException("A document corpus requires at least one case.");
        if (corpus.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != corpus.Cases.Count)
            throw new InvalidDataException("Document corpus case ids must be unique.");
        foreach (var item in corpus.Cases)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.DocumentPath))
                throw new InvalidDataException("Every document corpus case requires an id and document path.");
            if (item.RequiredFragments.Count == 0 || item.RequiredFragments.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Document corpus case '{item.Id}' requires non-empty oracle fragments.");
            if (item.ForbiddenFragments.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Document corpus case '{item.Id}' contains a blank forbidden fragment.");
        }
    }

    private static string ResolveDocument(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("Corpus document paths must be repository-relative.");
        var resolved = Path.GetFullPath(Path.Combine(root, relative));
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Corpus document path escapes repository root.");
        if (!File.Exists(resolved)) throw new FileNotFoundException("Corpus document was not found.", resolved);
        return resolved;
    }

    private static async Task WriteNewAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, value, Json, cancellationToken);
    }
}
