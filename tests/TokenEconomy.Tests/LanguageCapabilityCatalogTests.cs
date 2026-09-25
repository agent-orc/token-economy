using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class LanguageCapabilityCatalogTests
{
    internal static readonly DateTime At = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Seed_has_dated_provisional_research_for_all_models_and_both_languages_without_invented_measurements()
    {
        var catalog = LanguageCapabilityCatalog.Default;
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(new DateOnly(2026, 9, 25), catalog.AsOfDate);
        Assert.Equal(22, catalog.Records.Count);
        Assert.Equal(5, catalog.Studies.Count);
        Assert.All(catalog.Records.GroupBy(r => r.ModelId), group =>
            Assert.Equal(new[] { "de", "en" }, group.Select(r => r.Language).Order()));
        Assert.All(catalog.Records, row =>
        {
            Assert.Equal(row.ModelId, ModelPriceCatalog.Default.Find(row.ModelId)!.ModelId);
            Assert.Equal(LanguageEvidenceStatus.Provisional, row.Status);
            Assert.Equal(EffortLevel.Unspecified, row.ThinkingLevel);
            Assert.Null(row.Overall);
            Assert.Null(row.CostPerSampleUsd);
            Assert.All(Enum.GetValues<LanguageDimension>(), dimension => Assert.Null(row.Scores[dimension]));
            Assert.NotEmpty(row.Evidence);
            Assert.All(row.Evidence, evidence =>
            {
                Assert.Equal(DateTimeKind.Utc, evidence.ObservedAtUtc.Kind);
                Assert.Equal(LanguageEvidenceKind.Study, evidence.Kind);
                Assert.Contains(catalog.Studies, s => s.Url == evidence.Reference);
                Assert.NotEmpty(evidence.Note);
            });
        });
        Assert.All(catalog.Studies, s =>
        {
            Assert.Equal(LanguageEvidenceStatus.Provisional, s.Status);
            Assert.Equal(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), s.VerifiedAtUtc);
            Assert.StartsWith("https://", s.Url);
        });
    }

    [Fact]
    public void Lookup_uses_exact_language_effort_cutoff_and_latest_observation_not_best_score()
    {
        var first = Row("gpt-5.6-luna", .9m, .001m);
        var latest = Row("gpt-5.6-luna", .4m, .002m, "later") with
        {
            Evidence = [first.Evidence[0] with { ObservedAtUtc = At }],
        };
        var catalog = new LanguageCapabilityCatalog([first, latest]);
        Assert.Equal(latest.Id, catalog.Find("LUNA", "DE", EffortLevel.Medium, At)!.Id);
        Assert.Equal(first.Id, catalog.Find(KnownModels.Gpt56Luna, "de", EffortLevel.Medium, At.AddMinutes(-1))!.Id);
        Assert.Null(catalog.Find("luna", "en", EffortLevel.Medium, At));
        Assert.Null(catalog.Find("luna", "de", EffortLevel.High, At));
        Assert.Null(catalog.Find("unknown", "de"));
        Assert.Null(catalog.Find("luna", "de", atUtc: At.AddYears(-1)));
        Assert.Throws<ArgumentException>(() => catalog.Find("luna", "de", atUtc: DateTime.SpecifyKind(At, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Catalog_rejects_missing_provenance_invalid_scores_dates_and_duplicate_ids()
    {
        var row = Row("gpt-5.6-luna", .8m, .001m);
        var bad = new[]
        {
            row with { Evidence = [] }, row with { Provenance = null },
            row with { Scores = row.Scores with { Warmth = 1.01m } },
            row with { Overall = .9m }, row with { CostPerSampleUsd = -1m },
            row with { ThinkingLevel = EffortLevel.Unspecified }, row with { Language = "fr" },
            row with { ModelId = "luna" }, row with { CliId = "claude-code" },
            row with { Evidence = [row.Evidence[0] with { ObservedAtUtc = DateTime.SpecifyKind(At, DateTimeKind.Local) }] },
        };
        foreach (var invalid in bad)
            Assert.ThrowsAny<ArgumentException>(() => new LanguageCapabilityCatalog([invalid]));
        Assert.Throws<ArgumentException>(() => new LanguageCapabilityCatalog([row, row]));
        Assert.Throws<ArgumentException>(() => new LanguageCapabilityCatalog([row], schemaVersion: 2));
        Assert.Throws<ArgumentException>(() => new LanguageCapabilityCatalog([row], asOfDate: new DateOnly(2020, 1, 1)));
    }

    [Fact]
    public void All_thresholds_are_conjunctive_and_unknown_is_not_a_zero_score()
    {
        var row = Row("gpt-5.6-luna", .8m, 0m);
        var requirement = new HumanFriendlyLanguageRequirement
        {
            Language = "de", MinimumOverall = .8m,
            MinimumScores = new() { Readability = .8m, FactualRestraint = .81m },
        };
        Assert.False(requirement.IsSatisfiedBy(row));
        Assert.True((requirement with { MinimumScores = new() { FactualRestraint = .8m } }).IsSatisfiedBy(row));
        Assert.True((requirement with { MinimumOverall = null, MinimumScores = new() { Warmth = .8m } }).IsSatisfiedBy(row));
        Assert.False((requirement with { MinimumOverall = 0, MinimumScores = new(), AllowProvisional = true })
            .IsSatisfiedBy(LanguageCapabilityCatalog.Default.Find(row.ModelId, "de")));
        Assert.False((requirement with { AllowProvisional = true }).IsSatisfiedBy(row with { Status = LanguageEvidenceStatus.UnverifiedClaim }));
        Assert.Throws<ArgumentException>(() => (requirement with { MinimumOverall = null, MinimumScores = new() }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (requirement with { MinimumOverall = 2m }).Validate());
    }

    internal static LanguageCapabilityRecord Row(string model, decimal score, decimal cost, string? id = null) => new()
    {
        Id = id ?? model, ModelId = model, CliId = model.StartsWith("gpt-", StringComparison.Ordinal) ? "codex" : "claude-code",
        Language = "de", ThinkingLevel = EffortLevel.Medium, Status = LanguageEvidenceStatus.Observed,
        Scores = new() { Readability = score, Warmth = score, Directness = score, AbsenceOfAiIsms = score,
            ToneAdherence = score, FactualRestraint = score },
        Overall = score, CostPerSampleUsd = cost, Notes = "Synthetic test result; not production evidence.",
        Evidence = [new() { Kind = LanguageEvidenceKind.LocalBenchmark,
            ObservedAtUtc = At.AddDays(-1), Reference = "benchmarks/human-friendly-language/runs/test/", Note = "Test fixture." }],
        Provenance = new() { SourceRepository = "https://example.org/voice-lint", SourcePath = "benchmarks/human-friendly-language/results.json",
            SourceSha256 = new string('a', 64), MirrorPath = $"src/TokenEconomy/catalog/language-evidence/{new string('a', 64)}.json" },
    };

    internal static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "TokenEconomy.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
