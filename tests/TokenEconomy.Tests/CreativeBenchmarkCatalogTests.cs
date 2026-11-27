using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class CreativeBenchmarkCatalogTests
{
    private static readonly BenchmarkEvidenceCatalog Creative = BenchmarkEvidenceCatalog.Creative;

    private static BenchmarkResult Row(string id) => Assert.Single(Creative.Results, r => r.Id == id);

    [Fact]
    public void CreativeCatalogueUsesTheTe82SchemasAndStaysOutOfTheCodingCatalogue()
    {
        Assert.Equal(1, Creative.SchemaVersion);
        Assert.Equal(8, Creative.Types.Count);
        Assert.All(Creative.Types, type => Assert.Equal(BenchmarkCapabilityClass.CreativeBuild, type.CapabilityClass));
        Assert.All(Creative.Results, result =>
        {
            Assert.Equal(EvidenceClasses.Measured, result.EvidenceClass);
            Assert.Equal(new DateOnly(2026, 11, 27), result.RetrievedAt);
            Assert.NotNull(result.Context);
            Assert.NotNull(Creative.FindType(result.BenchmarkTypeId));
        });
        // Coding projections (website matrix) read only the default catalogue.
        Assert.DoesNotContain(BenchmarkEvidenceCatalog.Default.Types, type => type.CapabilityClass == BenchmarkCapabilityClass.CreativeBuild);
        Assert.Empty(BenchmarkEvidenceCatalog.Default.Results.Select(r => r.Id).Intersect(Creative.Results.Select(r => r.Id)));
    }

    [Fact]
    public void Aa3dClaudeRowsMatchTheSourceAndSonnetAndOpusAreLevelAtMax()
    {
        var fable = Row("te83-aa3d-claude-fable-5-2-max");
        var sonnet = Row("te83-aa3d-claude-sonnet-5-5-max");
        var opus = Row("te83-aa3d-claude-opus-5-5-max");
        Assert.Equal((1271.4151m, 25m), (fable.Score, fable.Context!.ConfidenceIntervalHalfWidth!.Value));
        Assert.Equal((1203.6913m, 28m), (sonnet.Score, sonnet.Context!.ConfidenceIntervalHalfWidth!.Value));
        Assert.Equal((1202.4312m, 28m), (opus.Score, opus.Context!.ConfidenceIntervalHalfWidth!.Value));
        Assert.True(Math.Abs(sonnet.Score - opus.Score) < sonnet.Context.ConfidenceIntervalHalfWidth!.Value);
        Assert.True(fable.Score - fable.Context.ConfidenceIntervalHalfWidth!.Value > sonnet.Score);
        Assert.All(new[] { fable, sonnet, opus }, row =>
        {
            Assert.Equal(EffortLevel.Max, row.ReasoningEffort);
            Assert.Equal(55, row.Context!.TaskCount);
            Assert.Equal(0.95m, row.Context.ConfidenceIntervalLevel);
            Assert.Equal(BenchmarkCostSource.Estimated, row.CostSource);
            Assert.Contains("lower bound", row.CostDerivation);
        });
    }

    [Fact]
    public void Aa3dHasNoRowsBelowMaxForClaudeOrOpenAiCandidates()
    {
        var rows = Creative.ResultsFor("aa-3d-v1.0-2026-11-27");
        Assert.DoesNotContain(rows, r => r.ModelId.Value.StartsWith("claude-", StringComparison.Ordinal) && r.ReasoningEffort != EffortLevel.Max);
        Assert.DoesNotContain(rows, r => r.ModelId.Value.StartsWith("gpt-", StringComparison.Ordinal) && r.ReasoningEffort != EffortLevel.Max);
        Assert.Contains(rows, r => r.ModelId.Value == "gpt-6.1-sol");
        Assert.Contains(rows, r => r.ModelId.Value == "gpt-6.2-astra" && r.Score > 1300m);
    }

    [Fact]
    public void Aa3dCoverageCountsAreRecordedAsAProxyNotAPlayableRate()
    {
        Assert.Contains("46 of 54", Row("te83-aa3d-claude-sonnet-5-5-max").Context!.SampleNotes);
        Assert.Contains("49 of 54", Row("te83-aa3d-claude-opus-5-5-max").Context!.SampleNotes);
        Assert.Contains("54 of 54", Row("te83-aa3d-claude-fable-5-2-max").Context!.SampleNotes);
        Assert.Contains("excluded share", Row("te83-aa3d-claude-opus-5-5-max").Context!.SampleNotes);
        Assert.Contains("excluded BEFORE voting", Creative.FindType("aa-3d-v1.0-2026-11-27")!.CitationNote);
    }

    [Fact]
    public void WebDevArenaRowsShowOpusMaxAheadOfSonnetOutsideTheIntervals()
    {
        var opus = Row("te83-webdev-claude-opus-5-5-max");
        var sonnetXHigh = Row("te83-webdev-claude-sonnet-5-5-xhigh");
        var sonnetHigh = Row("te83-webdev-claude-sonnet-5-5-high");
        var fable = Row("te83-webdev-claude-fable-5-2-xhigh");
        Assert.Equal(1820.35m, decimal.Round(opus.Score, 2));
        Assert.Equal(1772.64m, decimal.Round(sonnetXHigh.Score, 2));
        Assert.Equal(1715.42m, decimal.Round(sonnetHigh.Score, 2));
        Assert.Equal(1821.65m, decimal.Round(fable.Score, 2));
        Assert.True(opus.Score - opus.Context!.ConfidenceIntervalHalfWidth!.Value > sonnetXHigh.Score + sonnetXHigh.Context!.ConfidenceIntervalHalfWidth!.Value);
        Assert.InRange(opus.Score - sonnetHigh.Score, 100m, 110m);
        Assert.All(Creative.ResultsFor("arena-code-webdev-2026-11-23"), r =>
        {
            Assert.Equal(BenchmarkCostSource.Unavailable, r.CostSource);
            Assert.Equal(new DateOnly(2026, 11, 23), r.PublishedAt);
        });
        Assert.DoesNotContain(Creative.ResultsFor("arena-code-webdev-2026-11-23"),
            r => r.ModelId.Value == "claude-opus-5-5" && r.ReasoningEffort != EffortLevel.Max);
    }

    [Fact]
    public void Arena3dIsTheOnlySourceWithASonnetHighRowOnThreeDWork()
    {
        var sonnetHigh = Row("te83-arena3d-claude-sonnet-5-5-high");
        Assert.Equal((1817m, 16m), (sonnetHigh.Score, sonnetHigh.Context!.ConfidenceIntervalHalfWidth!.Value));
        Assert.Equal(1915m, Row("te83-arena3d-claude-opus-5-5-max").Score);
        Assert.DoesNotContain(Creative.Results, r => r.BenchmarkTypeId != "arena-code-webdev-3d-2026-11-23"
            && r.BenchmarkTypeId.Contains("3d", StringComparison.Ordinal)
            && r.ModelId.Value == "claude-sonnet-5-5" && r.ReasoningEffort == EffortLevel.High);
    }

    [Fact]
    public void DesignArenaRowsKeepTheUnpublishedDefaultEffortAsUnspecified()
    {
        Assert.Equal(EffortLevel.Unspecified, Row("te83-da-3d-design-claude-sonnet-5-5-unspecified").ReasoningEffort);
        var opusHigh = Row("te83-da-game-dev-agentic-claude-opus-5-5-high");
        Assert.Equal((EffortLevel.High, 1408m), (opusHigh.ReasoningEffort, opusHigh.Score));
        Assert.Equal(BenchmarkCostSource.Measured, opusHigh.CostSource);
        Assert.True(opusHigh.SecondaryMetrics!.CostPerTaskUsd > 3m);
    }

    [Fact]
    public void RubricMapsEachBenchmarkToTheDimensionsItMeasures()
    {
        var rubric = EffortDial.Default.CreativeRubric;
        Assert.Equal(new[] { "originality", "design", "gameFeel", "technicalExecution" }, rubric.Dimensions.Select(d => d.Id));
        Assert.Equal(Creative.Types.Select(t => t.Id).Order(), rubric.Benchmarks.Select(b => b.BenchmarkTypeId).Order());
        var aa = Assert.Single(rubric.Benchmarks, b => b.BenchmarkTypeId == "aa-3d-v1.0-2026-11-27");
        Assert.True(aa.ExcludesBrokenBuildsBeforeVote);
        Assert.False(aa.PublishesNonRunningShare);
        Assert.Equal("excluded", aa.Dimensions.TechnicalExecution);
        Assert.Equal(("measured", "measured", "partial"), (aa.Dimensions.Design, aa.Dimensions.GameFeel, aa.Dimensions.Originality));
        var webdev = Assert.Single(rubric.Benchmarks, b => b.BenchmarkTypeId == "arena-code-webdev-2026-11-23");
        Assert.False(webdev.ExcludesBrokenBuildsBeforeVote);
        Assert.Equal("none", webdev.PublishesCost);
    }

    [Fact]
    public void FleetEvidenceClassRequiresAnOwnRunContextWithACaseCount()
    {
        var type = BenchmarkEvidenceCatalog.Default.Types.First(t => t.AllowedUnpricedModelIds.Count == 0
            && !t.Id.StartsWith("cost-frontier-", StringComparison.Ordinal)
            && BenchmarkEvidenceCatalog.Default.ResultsFor(t.Id).Count > 0);
        var template = BenchmarkEvidenceCatalog.Default.Results.First(r => r.BenchmarkTypeId == type.Id);
        var fleetWithoutContext = template with { Id = "fleet-fixture-1", EvidenceClass = EvidenceClasses.Fleet, Context = null };
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog([type], [fleetWithoutContext]));

        var fleet = template with
        {
            Id = "fleet-fixture-2",
            EvidenceClass = EvidenceClasses.Fleet,
            Confidence = BenchmarkConfidence.OwnRun,
            Context = new BenchmarkEvidenceContext
            {
                SourceKind = "ownRun", SourcePublisher = "Agent Studio", DateBasis = "publishedDate",
                ObservedAt = template.PublishedAt, Harness = "Agent Studio", TaskCount = 12,
            },
        };
        var catalog = new BenchmarkEvidenceCatalog([type], [fleet]);
        Assert.Equal(EvidenceClasses.Fleet, Assert.Single(catalog.Results).EvidenceClass);
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog([type], [template with { Id = "x-1", EvidenceClass = "Q" }]));
    }
}
