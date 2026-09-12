using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class PublicCodeReviewBenchmarkTests
{
    [Fact]
    public void DirectReviewEvidenceHasItsOwnQueryableCapability()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var reviewTypes = catalog.Types.Where(t => t.CapabilityClass == BenchmarkCapabilityClass.CodeReview).ToArray();
        Assert.NotEmpty(reviewTypes);
        Assert.All(reviewTypes, type => Assert.NotEmpty(catalog.ResultsFor(type.Id)));
        Assert.Equal(BenchmarkCapabilityClass.CodingAgent, catalog.FindType("terminal-bench-v4.0-official-native-agents")!.CapabilityClass);
        Assert.All(catalog.Results.Where(r => r.Id.StartsWith("coderabbit-", StringComparison.Ordinal)), result =>
        {
            Assert.Equal(BenchmarkCapabilityClass.CodeReview, catalog.FindType(result.BenchmarkTypeId)!.CapabilityClass);
            Assert.Equal(BenchmarkConfidence.ThirdParty, result.Confidence);
            Assert.Equal("benchmarkOwner", result.Context!.SourceKind);
        });
    }

    [Fact]
    public void FableInternalSettingsRemainSeparateWithUnknownProviderEffort()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var results = catalog.Results.Where(r => r.Id.StartsWith("coderabbit-fable51-2026-09-01-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, results.Length);
        Assert.Equal(4, results.Select(r => r.BenchmarkTypeId).Distinct().Count());
        Assert.All(results, result =>
        {
            Assert.Equal(EffortLevel.Unspecified, result.ReasoningEffort);
            Assert.Equal("claude-fable-5-1", result.ModelId.Value);
            Assert.Equal(45, result.Context!.TaskCount);
            Assert.Null(result.Context.TrialCount);
            Assert.Null(result.Context.AttemptsPerTask);
            Assert.Contains("105", result.Context.SampleNotes);
            Assert.Contains("92 review-file", result.Context.SampleNotes);
            Assert.Null(result.SecondaryMetrics!.CostPerTaskUsd);
        });
        Assert.Equal(61m, catalog.ResultsFor("coderabbit-fable51-2026-09-01-low-recall").Single().Score);
        Assert.Equal(37.3m, catalog.ResultsFor("coderabbit-fable51-2026-09-01-low-precision").Single().Score);
        Assert.Equal(57.1m, catalog.ResultsFor("coderabbit-fable51-2026-09-01-high-recall").Single().Score);
        Assert.Equal(36.4m, catalog.ResultsFor("coderabbit-fable51-2026-09-01-high-precision").Single().Score);
    }

    [Fact]
    public void AstraSameStudyCoverageDoesNotInventPrecisionSampleSizeOrEffort()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var results = catalog.ResultsFor("coderabbit-astra-2026-09-04-overall-coverage");
        Assert.Equal(3, results.Count);
        Assert.Equal(61.3m, results.Single(r => r.ModelId.Value == "gpt-6-astra").Score);
        Assert.Equal(59m, results.Single(r => r.ModelId.Value == "gpt-5.6-sol").Score);
        Assert.Equal(50.2m, results.Single(r => r.ModelId.Value == "claude-opus-5").Score);
        Assert.All(results, result =>
        {
            Assert.Equal(EffortLevel.Unspecified, result.ReasoningEffort);
            Assert.Null(result.Context!.TaskCount);
            Assert.Null(result.Context.ConfidenceIntervalHalfWidth);
            Assert.Contains("precision is not reported", result.EvidenceExcerpt);
        });
    }

    [Fact]
    public void OpusEffortComparisonKeepsProfileStreamAndPatternCountExplicit()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var precision = catalog.ResultsFor("coderabbit-opus5-2026-07-24-senior-actionable-precision");
        Assert.Equal(35.6m, precision.Single(r => r.ReasoningEffort == EffortLevel.High).Score);
        Assert.Equal(39.3m, precision.Single(r => r.ReasoningEffort == EffortLevel.XHigh).Score);
        Assert.All(precision, result =>
        {
            Assert.Contains("senior-reviewer", result.Context!.Harness);
            Assert.Contains("96 evaluation patterns; three complete configuration repeats", result.Context.SampleNotes);
            Assert.Null(result.Context.TaskCount);
            Assert.Null(result.Context.TrialCount);
            Assert.Contains("https://www.coderabbit.ai/content/assets/opus-5-results-table.png", result.Context.AdditionalSourceUrls!);
        });
        var full = catalog.ResultsFor("coderabbit-opus5-2026-07-24-senior-full-stream-precision");
        Assert.Equal(28.6m, full.Single(r => r.ReasoningEffort == EffortLevel.XHigh).Score);
    }

    [Fact]
    public void KodusOriginalRatiosAndSnapshotDatePreserveDistinctFindingAndIssueDenominators()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        var type = catalog.FindType("kodus-light-v1-vendor-default-precision")!;
        Assert.Equal("finding precision ratio", type.Unit);
        Assert.Equal(0m, type.MinimumScore);
        Assert.Equal(1m, type.MaximumScore);
        var result = catalog.ResultsFor(type.Id).Single(r => r.ModelId.Value == "gpt-5.6-luna");
        Assert.Equal(0.5769230769230769m, result.Score);
        Assert.Equal(EffortLevel.Unspecified, result.ReasoningEffort);
        Assert.Equal(new DateOnly(2026, 9, 12), result.PublishedAt);
        Assert.Equal("firstObservedPublicSnapshot", result.Context!.DateBasis);
        Assert.Equal(30, result.Context.TaskCount);
        Assert.Contains("2026-08-04T19:56:24.224Z", result.Context.SampleNotes);
        Assert.Contains("28/95", result.EvidenceExcerpt);
        Assert.Contains("30/52", result.EvidenceExcerpt);
        Assert.Contains("531297bf50e5f065e3888d7e07b55dcacbf8df64", result.SourceUrl);
    }
}
