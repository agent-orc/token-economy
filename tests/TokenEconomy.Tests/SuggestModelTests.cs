using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class SuggestModelTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ModelEfficiencyMatrix Matrix = ModelEfficiencyMatrix.Default;

    private static readonly Cli[] Claude = [Cli.Claude];
    private static readonly Cli[] Codex = [Cli.Codex];
    private static readonly Cli[] Both = [Cli.Claude, Cli.Codex];

    private static IReadOnlyList<ModelSuggestion> Suggest(TaskClass task, BudgetPressure pressure, Cli[] clis)
        => Matrix.SuggestModel(task, pressure, clis, Now);

    private static int RankOf(IReadOnlyList<ModelSuggestion> ranked, string modelId)
    {
        for (var i = 0; i < ranked.Count; i++)
            if (ranked[i].ModelId == modelId) return i;
        return -1;
    }

    // ---- capability leads when the budget is comfortable ----

    [Fact]
    public void HeavyDesign_Comfortable_PicksAFrontierModel()
    {
        var top = Suggest(TaskClass.HeavyDesign, BudgetPressure.Comfortable, Claude)[0];
        Assert.Equal("claude-opus-4-8", top.ModelId);          // strongest coding default (declaration order)
        Assert.Equal(CapabilityTier.Frontier, top.Tier);
        Assert.Equal(Suitability.Ideal, top.Suitability);
        Assert.Equal(EffortLevel.High, top.SuggestedEffort);
    }

    [Fact]
    public void MechanicalChore_PicksTheLightEconomyModel_EvenWhenComfortable()
    {
        var top = Suggest(TaskClass.MechanicalChore, BudgetPressure.Comfortable, Claude)[0];
        Assert.Equal("claude-haiku-4-5", top.ModelId);
        Assert.Equal(CostClass.Economy, top.CostClass);
        Assert.Equal(Suitability.Ideal, top.Suitability);
        Assert.Equal(EffortLevel.Low, top.SuggestedEffort);
    }

    [Fact]
    public void Feature_Comfortable_PicksTheBalancedModelOverFrontier()
    {
        var ranked = Suggest(TaskClass.Feature, BudgetPressure.Comfortable, Claude);
        Assert.Equal("claude-sonnet-5", ranked[0].ModelId);
        Assert.Equal(Suitability.Ideal, ranked[0].Suitability);
        // A frontier Opus is only a "capable" fallback for a plain feature.
        var opus = ranked.Single(r => r.ModelId == "claude-opus-4-8");
        Assert.Equal(Suitability.Capable, opus.Suitability);
        Assert.True(ranked[0].Score > opus.Score);
    }

    // ---- budget pressure downshifts the ranking ----

    [Fact]
    public void Feature_Critical_DownshiftsFromPremiumToBalanced()
    {
        var ranked = Suggest(TaskClass.Feature, BudgetPressure.Critical, Claude);
        Assert.Equal("claude-sonnet-5", ranked[0].ModelId);
        Assert.Equal(CostClass.Standard, ranked[0].CostClass);

        // Under critical pressure the premium Opus is pushed well down the list.
        var opus = ranked.Single(r => r.ModelId == "claude-opus-4-8");
        Assert.True(ranked[0].Score > opus.Score);
        // Effort is trimmed one notch under critical pressure (Feature: Medium -> Low).
        Assert.Equal(EffortLevel.Low, ranked[0].SuggestedEffort);
    }

    [Fact]
    public void HeavyDesign_Critical_KeepsTheIdealFrontierOnTop_ButItsLeadErodes()
    {
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Critical, Claude);

        // The ideal frontier model still leads (capability fit is the first tiebreak)...
        Assert.Equal("claude-opus-4-8", ranked[0].ModelId);
        Assert.Equal(Suitability.Ideal, ranked[0].Suitability);

        // ...but a balanced, standard-cost model has caught up on score — the downshift is now on the
        // table for the admission algorithm to take.
        var sonnet = ranked.Single(r => r.ModelId == "claude-sonnet-5");
        Assert.Equal(ranked[0].Score, sonnet.Score);
        Assert.True(RankOf(ranked, "claude-sonnet-5") > 0);
    }

    [Fact]
    public void PressureNeverLetsAnUnderpoweredModelBeatACapableOne()
    {
        // Even the maximum economy bonus (Haiku, critical) cannot lift an underpowered model above a
        // capable one for heavy design.
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Critical, Claude);
        var haiku = ranked.Single(r => r.ModelId == "claude-haiku-4-5");
        var sonnet = ranked.Single(r => r.ModelId == "claude-sonnet-5");
        Assert.Equal(Suitability.Underpowered, haiku.Suitability);
        Assert.True(sonnet.Score > haiku.Score);
    }

    // ---- CLI availability ----

    [Fact]
    public void OnlyModelsOfAvailableClisAreSuggested()
    {
        var claudeOnly = Suggest(TaskClass.Feature, BudgetPressure.Comfortable, Claude);
        Assert.All(claudeOnly, s => Assert.Equal(Cli.Claude, s.Cli));
        Assert.DoesNotContain(claudeOnly, s => s.ModelId.StartsWith("gpt"));

        var codexOnly = Suggest(TaskClass.Feature, BudgetPressure.Comfortable, Codex);
        Assert.NotEmpty(codexOnly);
        Assert.All(codexOnly, s => Assert.Equal(Cli.Codex, s.Cli));
        Assert.All(codexOnly, s => Assert.StartsWith("gpt", s.ModelId));
    }

    [Fact]
    public void NoAvailableCli_YieldsEmpty_SoTheCallerWaits()
    {
        Assert.Empty(Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Comfortable, [], Now));
        Assert.Empty(Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Comfortable, null, Now));
    }

    // ---- restricted / deprecated / unknown-cost handling ----

    [Fact]
    public void RestrictedAndDeprecatedModels_AreNeverSuggested()
    {
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Comfortable, Both);
        Assert.DoesNotContain(ranked, s => s.ModelId == "claude-mythos-5");   // Glasswing-only
        Assert.DoesNotContain(ranked, s => s.ModelId == "claude-opus-4-1");   // deprecated
    }

    [Fact]
    public void KnownCostBeatsUnprojectableCost_AtEqualScore()
    {
        // Opus (premium, priced) and gpt-5.6 (unpriced) both score 95 for heavy design when comfortable;
        // the model whose cost can be projected ranks first.
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Comfortable, Both);
        var opus = ranked.Single(r => r.ModelId == "claude-opus-4-8");
        var gpt = ranked.Single(r => r.ModelId == "gpt-5.6");
        Assert.Equal(opus.Score, gpt.Score);
        Assert.True(RankOf(ranked, "claude-opus-4-8") < RankOf(ranked, "gpt-5.6"));
        Assert.Equal(CostClass.Unknown, gpt.CostClass);
    }

    [Fact]
    public void UnknownCost_IsDeprioritisedUnderPressure()
    {
        // With budget critical, an unpriced frontier model falls behind a priced economy/standard model
        // for a feature — you should not route a pressured run onto uncosted spend.
        var ranked = Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Critical, [Cli.Claude, Cli.Codex], Now);
        var sonnet = ranked.Single(r => r.ModelId == "claude-sonnet-5");
        var gpt = ranked.Single(r => r.ModelId == "gpt-5.6");
        Assert.True(sonnet.Score > gpt.Score);
    }

    [Fact]
    public void UnconfirmedCost_SurfacesOnTheSuggestion()
    {
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Comfortable, Claude);
        Assert.True(ranked.Single(r => r.ModelId == "claude-opus-4-5").CostUnconfirmed);
        Assert.False(ranked.Single(r => r.ModelId == "claude-opus-4-8").CostUnconfirmed);
    }

    // ---- effort clamping ----

    [Fact]
    public void SuggestedEffort_IsClampedToWhatTheModelSupports()
    {
        // Heavy design wants High effort, but Haiku only supports up to Medium.
        var ranked = Suggest(TaskClass.HeavyDesign, BudgetPressure.Comfortable, Claude);
        var haiku = ranked.Single(r => r.ModelId == "claude-haiku-4-5");
        Assert.Equal(EffortLevel.Medium, haiku.SuggestedEffort);
    }

    // ---- rationale + determinism ----

    [Fact]
    public void EverySuggestion_CarriesARationale()
    {
        foreach (var s in Suggest(TaskClass.Feature, BudgetPressure.Tight, Both))
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Rationale));
            Assert.Contains(s.ModelId, s.Rationale);
            Assert.Contains("feature work", s.Rationale);
            Assert.Contains("effort", s.Rationale);
        }
    }

    [Fact]
    public void Rationale_NamesTheBudgetPressureWhenNotComfortable()
    {
        var critical = Suggest(TaskClass.Feature, BudgetPressure.Critical, Claude)[0];
        Assert.Contains("critical", critical.Rationale);

        var comfortable = Suggest(TaskClass.Feature, BudgetPressure.Comfortable, Claude)[0];
        Assert.DoesNotContain("pressure", comfortable.Rationale);
    }

    [Fact]
    public void Rationale_ReadsAsAWholeSentence()
    {
        // This is the exact string quoted in README.md — keep them in lock-step.
        var best = Suggest(TaskClass.Feature, BudgetPressure.Tight, Claude)[0];
        Assert.Equal("claude-sonnet-5", best.ModelId);
        Assert.Equal(EffortLevel.Medium, best.SuggestedEffort);
        Assert.Equal(
            "claude-sonnet-5: balanced tier, an ideal match for feature work; standard cost — moderate spend under tight pressure. Suggested effort: medium.",
            best.Rationale);
    }

    [Fact]
    public void SuggestModel_IsDeterministic()
    {
        var a = Suggest(TaskClass.Feature, BudgetPressure.Tight, Both);
        var b = Suggest(TaskClass.Feature, BudgetPressure.Tight, Both);
        Assert.Equal(a.Select(x => x.ModelId), b.Select(x => x.ModelId));
        Assert.Equal(a, b);   // record value-equality, element by element
    }

    [Fact]
    public void Results_AreSortedByScoreDescending()
    {
        var ranked = Suggest(TaskClass.Feature, BudgetPressure.Tight, Both);
        for (var i = 1; i < ranked.Count; i++)
            Assert.True(ranked[i - 1].Score >= ranked[i].Score);
    }
}
