using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class EffortDialTests
{
    private static readonly EffortDial Dial = EffortDial.Default;
    private static readonly DialCli[] Clis = [DialCli.Claude, DialCli.Codex];

    [Fact]
    public void CatalogueDefinesTheOwnerWorkClassesWithBothCliLadders()
    {
        Assert.Equal(
            new[] { "mechanical", "feature-logic", "ui-visual", "hard", "review-aspect", "review-decision", "research", "summary", "orchestrator-chat", "creative" },
            Dial.WorkClasses.Select(c => c.Id));
        Assert.Equal(new[] { "a", "b1", "b2", "c", "d", "e", "f", "g", "h", "i" }, Dial.WorkClasses.Select(c => c.Letter));
        Assert.Equal("frugalToStrong", Dial.Decision.Semantics);
        Assert.Equal(EffortLevel.Medium, Dial.Decision.StandardEffort);
        Assert.Equal(EffortLevel.High, Dial.Decision.HardWorkEffort);
        Assert.Equal(50, Dial.DefaultPosition);
        Assert.Equal(new DateOnly(2026, 11, 27), Dial.AsOfDate);
    }

    [Theory]
    [InlineData("mechanical", "mechanical.claude.opus-5-5-low", "mechanical.codex.gpt-6.1-sol-low")]
    [InlineData("feature-logic", "feature-logic.claude.opus-5-5-medium", "feature-logic.codex.gpt-6.1-sol-medium")]
    [InlineData("ui-visual", "ui-visual.claude.opus-5-5-medium", "ui-visual.codex.gpt-6.1-sol-medium")]
    [InlineData("hard", "hard.claude.opus-5-5-high", "hard.codex.gpt-6.1-sol-high")]
    [InlineData("review-aspect", "review-aspect.claude.opus-5-5-low", "review-aspect.codex.gpt-6.1-sol-low")]
    [InlineData("review-decision", "review-decision.claude.opus-5-5-low", "review-decision.codex.gpt-6.1-sol-low")]
    [InlineData("research", "research.claude.opus-5-5-medium", "research.codex.gpt-6.1-sol-medium")]
    [InlineData("summary", "summary.claude.haiku-5-5-medium", "summary.codex.gpt-6-luna-high")]
    [InlineData("orchestrator-chat", "orchestrator-chat.claude.opus-5-5-medium", "orchestrator-chat.codex.gpt-6.1-sol-medium")]
    [InlineData("creative", "creative.claude.sonnet-5-5-high", "creative.codex.gpt-6.1-sol-max")]
    public void DefaultPositionRunsTheDecidedStandardRungs(string workClass, string claudeRung, string codexRung)
    {
        Assert.Equal(claudeRung, Dial.Pick(Dial.DefaultPosition, workClass, DialCli.Claude).Rung.Id);
        Assert.Equal(codexRung, Dial.Pick(Dial.DefaultPosition, workClass, DialCli.Codex).Rung.Id);
    }

    [Fact]
    public void DefaultPositionUsesMediumForNormalCardsHighForHardCardsAndNoXHigh()
    {
        foreach (var cli in Clis)
        {
            Assert.Equal(EffortLevel.Medium, Dial.Pick(50, "feature-logic", cli).Rung.Effort);
            Assert.Equal(EffortLevel.Medium, Dial.Pick(50, "ui-visual", cli).Rung.Effort);
            Assert.Equal(EffortLevel.Medium, Dial.Pick(50, "research", cli).Rung.Effort);
            Assert.Equal(EffortLevel.High, Dial.Pick(50, "hard", cli).Rung.Effort);
            Assert.DoesNotContain(Dial.PicksAt(50, cli), pick => pick.Rung.Effort == EffortLevel.XHigh);
        }
        Assert.Equal(("claude-opus-5-5", EffortLevel.Low),
            (Dial.Pick(50, "review-aspect", DialCli.Claude).Rung.ModelId, Dial.Pick(50, "review-aspect", DialCli.Claude).Rung.Effort));
    }

    [Fact]
    public void PickIsDeterministicAndNeverStepsDownAsThePositionRises()
    {
        foreach (var workClass in Dial.WorkClasses)
        foreach (var cli in Clis)
        {
            var previous = -1;
            for (var position = 0; position <= 100; position++)
            {
                var pick = Dial.Pick(position, workClass.Id, cli);
                Assert.Equal(pick, Dial.Pick(position, workClass.Id, cli) with { SkippedRestrictedRungIds = pick.SkippedRestrictedRungIds });
                Assert.True(pick.RungIndex >= previous, $"{workClass.Id}/{cli} steps down at {position}");
                Assert.False(pick.Rung.EscalationOnly);
                Assert.True(Dial.IsSelectable(pick.Rung));
                previous = pick.RungIndex;
            }
            Assert.Equal(0, Dial.Pick(0, workClass.Id, cli).RungIndex);
            var banded = workClass.Ladder(cli).Rungs.Where(r => !r.EscalationOnly).ToArray();
            Assert.Equal(banded[^1].Id, Dial.Pick(100, workClass.Id, cli).Rung.Id);
        }
    }

    [Fact]
    public void BandBoundariesAreInclusiveAtFromPosition()
    {
        Assert.Equal("feature-logic.claude.opus-5-5-medium", Dial.Pick(74, "feature-logic", DialCli.Claude).Rung.Id);
        Assert.Equal("feature-logic.claude.opus-5-5-high", Dial.Pick(75, "feature-logic", DialCli.Claude).Rung.Id);
        Assert.Equal("mechanical.claude.haiku-5-5-medium", Dial.Pick(19, "mechanical", DialCli.Claude).Rung.Id);
        Assert.Equal("mechanical.claude.opus-5-5-low", Dial.Pick(20, "mechanical", DialCli.Claude).Rung.Id);
    }

    [Fact]
    public void InvalidInputsThrowInsteadOfGuessing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Dial.Pick(-1, "feature-logic", DialCli.Claude));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dial.Pick(101, "feature-logic", DialCli.Claude));
        Assert.Throws<ArgumentException>(() => Dial.Pick(50, "unknown-class", DialCli.Claude));
        Assert.Throws<ArgumentException>(() => Dial.Escalate("hard", DialCli.Claude, "feature-logic.claude.opus-5-5-high"));
    }

    [Fact]
    public void ConditionalFloorsRaiseThePickAndReportTheFloor()
    {
        var docs = Dial.Pick(0, "mechanical", DialCli.Claude);
        Assert.Equal("claude-haiku-5-5", docs.Rung.ModelId);
        Assert.Null(docs.AppliedFloor);

        var code = Dial.Pick(0, "mechanical", DialCli.Claude, ["code-change"]);
        Assert.Equal("mechanical.claude.opus-5-5-low", code.Rung.Id);
        Assert.Equal("mechanical.claude.haiku-5-5-medium", code.BandRungId);
        Assert.Equal("code-change", code.AppliedFloor!.Condition);

        var security = Dial.Pick(0, "review-aspect", DialCli.Claude, ["security-relevant"]);
        Assert.Equal("review-aspect.claude.opus-5-5-medium", security.Rung.Id);
        Assert.Equal("review-aspect.codex.gpt-6.1-sol-medium", Dial.Pick(0, "review-aspect", DialCli.Codex, ["security-relevant"]).Rung.Id);
        Assert.Equal("review-decision.claude.opus-5-5-medium", Dial.Pick(10, "review-decision", DialCli.Claude, ["security-relevant"]).Rung.Id);

        Assert.Equal("feature-logic.claude.opus-5-5-high", Dial.Pick(0, "feature-logic", DialCli.Claude, ["correctness-critical"]).Rung.Id);
        Assert.Equal("feature-logic.codex.gpt-6.1-sol-high", Dial.Pick(0, "feature-logic", DialCli.Codex, ["correctness-critical"]).Rung.Id);

        // A floor never lowers a stronger band and an unrelated condition changes nothing.
        var strong = Dial.Pick(95, "review-aspect", DialCli.Claude, ["security-relevant"]);
        Assert.Equal("review-aspect.claude.opus-5-5-medium", strong.Rung.Id);
        Assert.Null(strong.AppliedFloor);
        Assert.Equal(Dial.Pick(0, "research", DialCli.Codex).Rung.Id, Dial.Pick(0, "research", DialCli.Codex, ["security-relevant"]).Rung.Id);
    }

    [Fact]
    public void HardWorkHasAnUnconditionalHighFloor()
    {
        foreach (var cli in Clis)
        {
            var floor = Assert.Single(Dial.FindWorkClass("hard")!.Floors, f => f.Cli == (cli == DialCli.Claude ? "claude" : "codex"));
            Assert.Null(floor.Condition);
            for (var position = 0; position <= 100; position += 5)
                Assert.True(Dial.Pick(position, "hard", cli).Rung.Effort >= EffortLevel.High);
        }
    }

    [Fact]
    public void EscalationOnlyRungsAreReachedOnlyAfterTheBandedTopFails()
    {
        var fable = Dial.Escalate("hard", DialCli.Claude, "hard.claude.opus-5-5-xhigh");
        Assert.Equal("hard.claude.fable-5-2-high", fable!.Id);
        Assert.True(fable.EscalationOnly);
        Assert.False(Dial.IsSelectable(fable)); // restricted in routing until fleet compatibility is observed
        Assert.Null(Dial.Escalate("hard", DialCli.Claude, fable.Id));
        Assert.Equal("hard.codex.gpt-6.2-astra-high", Dial.Escalate("hard", DialCli.Codex, "hard.codex.gpt-6.1-sol-xhigh")!.Id);
        Assert.Equal("hard.claude.opus-5-5-xhigh", Dial.Escalate("hard", DialCli.Claude, "hard.claude.opus-5-5-high")!.Id);
    }

    [Fact]
    public void RestrictedBandedRungIsSkippedDownwardsButNotBelowTheFloor()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        var hard = json["workClasses"]!.AsArray().First(c => c!["id"]!.GetValue<string>() == "hard")!;
        var fable = hard["ladders"]!["claude"]!["rungs"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "hard.claude.fable-5-2-high")!;
        fable.AsObject().Remove("escalationOnly");
        fable["fromPosition"] = 90;
        var dial = EffortDial.Parse(json.ToJsonString());

        var pick = dial.Pick(95, "hard", DialCli.Claude);
        Assert.Equal("hard.claude.opus-5-5-xhigh", pick.Rung.Id);
        Assert.Equal("hard.claude.fable-5-2-high", pick.BandRungId);
        Assert.Equal(new[] { "hard.claude.fable-5-2-high" }, pick.SkippedRestrictedRungIds);
    }

    [Fact]
    public void InverseViewListsEveryClassWithItsExpectedCost()
    {
        foreach (var cli in Clis)
        {
            var picks = Dial.PicksAt(50, cli);
            Assert.Equal(Dial.WorkClasses.Select(c => c.Id), picks.Select(p => p.WorkClassId));
            var costs = Dial.ExpectedCostsAt(50, cli);
            Assert.Equal(Dial.WorkClasses.Count, costs.Count);
            Assert.All(picks, pick => Assert.Same(Dial.CostOf(pick.Rung.Id), costs[pick.WorkClassId]));
        }
    }

    [Fact]
    public void FleetAnchoredClaudeCostsUseListPriceAsAFlaggedProxy()
    {
        var medium = Dial.Pick(50, "feature-logic", DialCli.Claude).Cost;
        Assert.Equal(DialCostStatus.Estimated, medium.Status);
        Assert.Equal(DialCostUnit.AcceptedCard, medium.Unit);
        Assert.Equal(4.57m, medium.ListUsdLow);
        Assert.Equal(4.57m, medium.ListUsdHigh);
        Assert.Null(medium.CodexCreditsLow);
        Assert.Equal(DialQuotaBasis.ClaudeListPriceProxy, medium.QuotaBasis);
        Assert.Null(medium.MeasuredQuotaPointsPerAcceptedCard);
        Assert.Contains(medium.Derivation, line => line.Contains("flagged proxy", StringComparison.Ordinal));
        Assert.Contains(medium.Derivation, line => line.Contains("[F]", StringComparison.Ordinal));
        Assert.All(Dial.WorkClasses.SelectMany(c => c.Ladder(DialCli.Claude).Rungs), rung => Assert.Null(rung.MeasuredQuotaWeight));
    }

    [Fact]
    public void CachePriceIsTheLeverBetweenGpt6SolAndGpt61Sol()
    {
        // Same token profile, 74% cache-read share measured on gpt-6-sol high: 0.74 x 0.5 + 0.26 x 1 = 0.63.
        var cost = Dial.Pick(0, "hard", DialCli.Codex).Cost;
        Assert.Equal("gpt-6.1-sol", Dial.Pick(0, "hard", DialCli.Codex).Rung.ModelId);
        Assert.Equal(6.804m, cost.ListUsdLow);
        Assert.Equal(cost.ListUsdLow, cost.ListUsdHigh);
        Assert.True(cost.ListUsdHigh < Dial.FindFleetRow("fleet-gpt-6-sol-high")!.MedianListUsdPerAcceptedCard);
        Assert.Equal(DialQuotaBasis.CodexCreditRate, cost.QuotaBasis);
        Assert.Equal(170.10m, cost.CodexCreditsLow); // official credit rate: list price x 25
        Assert.Equal(cost.ListUsdLow * 25, cost.CodexCreditsLow);
    }

    [Fact]
    public void RangesWidenWhereTheCacheShareOrEffortStepIsAssumed()
    {
        var medium = Dial.Pick(0, "feature-logic", DialCli.Codex).Cost;
        Assert.Equal(4.7438m, medium.ListUsdLow);  // 8.25 x (0.85 x 0.5 + 0.15)
        Assert.Equal(5.5688m, medium.ListUsdHigh); // 8.25 x (0.65 x 0.5 + 0.35)
        var xhigh = Dial.CostOf("hard.claude.opus-5-5-xhigh");
        Assert.True(xhigh.ListUsdLow > 9.65m && xhigh.ListUsdHigh > xhigh.ListUsdLow);
        Assert.All(Dial.WorkClasses.SelectMany(c => Clis.SelectMany(cli => c.Ladder(cli).Rungs)), rung =>
        {
            var c = Dial.CostOf(rung.Id);
            if (c.Status == DialCostStatus.Estimated)
                Assert.True(c.ListUsdLow > 0 && c.ListUsdHigh >= c.ListUsdLow, rung.Id);
        });
    }

    [Fact]
    public void ClassesWithoutAnAnchorReportUnknownNotZero()
    {
        foreach (var workClass in new[] { "summary", "orchestrator-chat" })
        foreach (var cli in Clis)
        {
            var cost = Dial.Pick(50, workClass, cli).Cost;
            Assert.Equal(DialCostStatus.Unknown, cost.Status);
            Assert.Null(cost.ListUsdLow);
            Assert.Null(cost.ListUsdHigh);
            Assert.False(string.IsNullOrWhiteSpace(cost.UnknownReason));
        }
    }

    [Fact]
    public void CreativeCostIsPerBenchmarkTaskBecauseNoFleetCohortExists()
    {
        var sonnetMax = Dial.CostOf("creative.claude.sonnet-5-5-max");
        var opusMax = Dial.CostOf("creative.claude.opus-5-5-max");
        Assert.Equal(DialCostUnit.BenchmarkTask, sonnetMax.Unit);
        Assert.True(sonnetMax.ListUsdHigh < opusMax.ListUsdLow);
        Assert.InRange(sonnetMax.ListUsdLow!.Value / opusMax.ListUsdLow!.Value, 0.6m, 0.75m);
        Assert.Equal(sonnetMax.ListUsdHigh, Dial.CostOf("creative.claude.sonnet-5-5-high").ListUsdHigh);
    }

    [Fact]
    public void FleetCohortIsAnonymisedWithWindowAndCaseCounts()
    {
        var cohort = Assert.Single(Dial.FleetCohorts);
        Assert.Equal(EvidenceClasses.Fleet, cohort.EvidenceClass);
        Assert.Equal(new DateOnly(2026, 10, 20), cohort.WindowStart);
        Assert.Equal(new DateOnly(2026, 11, 17), cohort.WindowEnd);
        Assert.Equal(28, cohort.WindowEnd.DayNumber - cohort.WindowStart.DayNumber);
        Assert.Equal(0.026m, cohort.RetryTaxShareOfSpend);
        Assert.Contains(cohort.Caveats, c => c.Contains("not assigned at random", StringComparison.Ordinal));
        var expected = new (string Model, EffortLevel Effort, int Cards, int Accepted, int Passed, int Reviewed, decimal Usd, long Tokens)[]
        {
            ("claude-opus-5-5", EffortLevel.Low, 16, 16, 16, 16, 1.58m, 3_000_000),
            ("claude-opus-5-5", EffortLevel.Medium, 51, 42, 50, 51, 4.57m, 11_900_000),
            ("claude-opus-5-5", EffortLevel.High, 77, 75, 69, 76, 9.65m, 25_500_000),
            ("gpt-6-sol", EffortLevel.Medium, 92, 67, 74, 75, 8.25m, 36_600_000),
            ("gpt-6-sol", EffortLevel.High, 219, 199, 175, 205, 10.80m, 44_900_000),
            ("gpt-6-sol", EffortLevel.XHigh, 16, 16, 12, 16, 14.53m, 59_600_000),
        };
        Assert.Equal(expected, cohort.Rows.Select(r => (r.ModelId, r.Effort, r.Cards, r.Accepted, r.FirstReviewPassed!.Value, r.FirstReviewed!.Value, r.MedianListUsdPerAcceptedCard, r.MedianTokensPerAcceptedCard!.Value)));
        var cache = Assert.Single(cohort.CacheReadObservations, o => o.CacheReadShareOfListCost is not null);
        Assert.Equal((17_700_000_000L, 0.74m), (cache.CachedReadTokens!.Value, cache.CacheReadShareOfListCost!.Value));
        Assert.Equal(7_100_000_000L, cohort.CacheReadObservations.Single(o => o.ModelId == "claude-opus-5-5").CachedReadTokens);
    }

    [Fact]
    public void LoaderRejectsFleetEvidenceThatNamesACard()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        json["fleetCohorts"]![0]!["caveats"]!.AsArray().Add("Outlier ABC-123 removed.");
        var error = Assert.Throws<InvalidOperationException>(() => EffortDial.Parse(json.ToJsonString()));
        Assert.Contains("anonymised", error.Message);
    }

    [Fact]
    public void LoaderRejectsFleetEvidenceWithoutAWindow()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        json["fleetCohorts"]![0]!["windowEnd"] = "2026-10-20";
        Assert.Throws<InvalidOperationException>(() => EffortDial.Parse(json.ToJsonString()));
    }

    [Fact]
    public void LoaderRejectsLightTierModelsOutsideMechanicalAndSummaryWork()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        var research = json["workClasses"]!.AsArray().First(c => c!["id"]!.GetValue<string>() == "research")!;
        var rung = research["ladders"]!["claude"]!["rungs"]![0]!;
        rung["modelId"] = "claude-haiku-5-5";
        var error = Assert.Throws<InvalidOperationException>(() => EffortDial.Parse(json.ToJsonString()));
        Assert.Contains("Light-tier", error.Message);
    }

    [Fact]
    public void LoaderRejectsCodingEvidenceAsCreativeQualityEvidence()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        var creative = json["workClasses"]!.AsArray().First(c => c!["id"]!.GetValue<string>() == "creative")!;
        creative["ladders"]!["claude"]!["rungs"]![0]!["evidenceIds"]!.AsArray().Add("tb41-cost-per-trial");
        var error = Assert.Throws<InvalidOperationException>(() => EffortDial.Parse(json.ToJsonString()));
        Assert.Contains("non-creative", error.Message);
    }

    [Fact]
    public void LoaderRejectsALadderThatLowersEffortOnTheSameModel()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        var feature = json["workClasses"]!.AsArray().First(c => c!["id"]!.GetValue<string>() == "feature-logic")!;
        feature["ladders"]!["claude"]!["rungs"]![1]!["effort"] = "low";
        Assert.Throws<InvalidOperationException>(() => EffortDial.Parse(json.ToJsonString()));
    }

    [Fact]
    public void EveryRungCitesResolvableEvidenceAndEveryCreativeRungStatesAllFourDimensions()
    {
        foreach (var workClass in Dial.WorkClasses)
        foreach (var rung in Clis.SelectMany(cli => workClass.Ladder(cli).Rungs))
        {
            Assert.NotEmpty(rung.EvidenceIds);
            Assert.All(rung.EvidenceIds, id => Assert.NotNull(Dial.FindEvidence(id)));
            if (workClass.Id == "creative")
            {
                Assert.NotNull(rung.Dimensions);
                Assert.All(rung.EvidenceIds, id => Assert.Contains(Dial.FindEvidence(id)!.Scope, new[] { "creative", "fleet" }));
            }
            else
            {
                Assert.Null(rung.Dimensions);
            }
        }
    }

    [Fact]
    public void ConclusionsNameTheClassesTheyApplyTo()
    {
        var sonnetCoding = Assert.Single(Dial.Conclusions, c => c.Id == "sonnet-not-cheap-opus-on-coding");
        Assert.Contains("creative", sonnetCoding.DoesNotApplyTo);
        Assert.Contains("ui-visual", sonnetCoding.DoesNotApplyTo);
        Assert.Contains("feature-logic", sonnetCoding.AppliesTo);
        Assert.DoesNotContain(Dial.ConclusionsFor("creative"), c => c.Id == sonnetCoding.Id);
        Assert.Contains(Dial.ConclusionsFor("creative"), c => c.Id == "sonnet-level-with-opus-on-3d-at-max");
        Assert.Contains(Dial.ConclusionsFor("creative"), c => c.Id == "preference-arenas-do-not-measure-playability");
        Assert.All(Dial.Conclusions, c => Assert.Empty(c.AppliesTo.Intersect(c.DoesNotApplyTo)));
    }

    [Fact]
    public void SonnetHighAgainstOpusLowStaysARecordedGap()
    {
        var creative = Dial.FindWorkClass("creative")!;
        var opusLow = Assert.Single(creative.Ladders.Claude.Candidates, c => c.ModelId == "claude-opus-5-5" && c.Effort == EffortLevel.Low);
        Assert.Empty(opusLow.EvidenceIds);
        Assert.Contains("thin evidence", opusLow.Reason);
        Assert.Contains(creative.EvidenceGaps, gap => gap.Contains("Sonnet 5.5 high against Opus 5.5 low", StringComparison.Ordinal));
        var sonnetHigh = creative.Ladders.Claude.Rungs[0];
        Assert.Equal(("claude-sonnet-5-5", EffortLevel.High), (sonnetHigh.ModelId, sonnetHigh.Effort));
        Assert.Equal("none", sonnetHigh.Dimensions!.TechnicalExecution.Status);
    }

    [Fact]
    public void FleetSectionsOfTheDialCarryNoCardKeys()
    {
        var json = JsonNode.Parse(EffortDial.EmbeddedJson())!;
        var fleetText = json["fleetCohorts"]!.ToJsonString() + string.Concat(json["evidence"]!.AsArray()
            .Where(e => e!["evidenceClass"]!.GetValue<string>() == EvidenceClasses.Fleet).Select(e => e!.ToJsonString()));
        Assert.DoesNotMatch(new Regex(@"\b(?!GPT-)[A-Z][A-Z0-9]{1,9}-\d+\b"), fleetText);
    }
}
