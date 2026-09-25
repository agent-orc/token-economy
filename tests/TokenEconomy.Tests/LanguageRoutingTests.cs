using TokenEconomy;
using Xunit;
using static TokenEconomy.Tests.LanguageCapabilityCatalogTests;

namespace TokenEconomy.Tests;

public sealed class LanguageRoutingTests
{
    private static readonly HumanFriendlyLanguageRequirement Requirement = new()
    {
        Language = "de", MinimumOverall = .75m, ThinkingLevel = EffortLevel.Medium,
    };
    private static ModelEfficiencyMatrix Matrix(params LanguageCapabilityRecord[] records) =>
        new(ModelPriceCatalog.Default, ModelEfficiencyMatrix.Default.Profiles, new LanguageCapabilityCatalog(records));

    [Fact]
    public void Constraint_excludes_below_threshold_and_ranks_actual_sample_cost_before_tier_or_price_band()
    {
        var matrix = Matrix(Row("gpt-5.6-luna", .9m, .09m), Row("gpt-6-luna", .7m, .00001m),
            Row("gpt-5.6-sol", .8m, .001m));
        var candidates = matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex, Cli.ClaudeCode], At, Requirement);
        Assert.Equal(new[] { "gpt-5.6-sol", "gpt-5.6-luna" }, candidates.Select(c => c.ModelId));
        Assert.All(candidates, c =>
        {
            Assert.True(Requirement.IsSatisfiedBy(c.LanguageCapability));
            Assert.Equal(EffortLevel.Medium, c.SuggestedEffort);
            Assert.Contains(c.LanguageCapability!.Id, c.Rationale);
        });
        Assert.Null(matrix.EvaluateModel("gpt-6-luna", TaskClass.DocEdit, BudgetPressure.Tight, At, requirement: Requirement));
        Assert.NotNull(matrix.EvaluateModel(KnownModels.Gpt56Sol, TaskClass.DocEdit, BudgetPressure.Tight, At, requirement: Requirement));
        Assert.Null(matrix.EvaluateModel(KnownModels.Gpt56Sol, TaskClass.DocEdit, BudgetPressure.Tight, At,
            EffortLevel.High, Requirement));
    }

    [Fact]
    public void Equal_costs_keep_established_preference_and_no_constraint_keeps_original_ranking()
    {
        var matrix = Matrix(Row("gpt-5.6-luna", .8m, .001m), Row("gpt-6-luna", .9m, .001m));
        var original = ModelEfficiencyMatrix.Default.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At);
        var unconstrained = matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At);
        Assert.Equal(original, unconstrained);
        var expected = original.Where(c => c.ModelId is "gpt-5.6-luna" or "gpt-6-luna").Select(c => c.ModelId);
        Assert.Equal(expected, matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At, Requirement).Select(c => c.ModelId));
    }

    [Fact]
    public void Language_and_effort_do_not_transfer_and_future_evidence_is_excluded()
    {
        var matrix = Matrix(Row("gpt-5.6-luna", .9m, .001m));
        Assert.Empty(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At, Requirement with { Language = "en" }));
        Assert.Empty(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At, Requirement with { ThinkingLevel = EffortLevel.High }));
        Assert.Empty(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At.AddYears(-1), Requirement));
        Assert.Empty(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.ClaudeCode], At, Requirement));
        Assert.Empty(matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Critical, [Cli.Codex], At, Requirement));
    }

    [Fact]
    public void Provisional_measurements_require_opt_in_and_unknown_seed_never_passes()
    {
        var row = Row("gpt-5.6-luna", .8m, .001m) with { Status = LanguageEvidenceStatus.Provisional };
        var matrix = Matrix(row);
        Assert.Empty(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At, Requirement));
        Assert.Single(matrix.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight, [Cli.Codex], At, Requirement with { AllowProvisional = true }));
        Assert.Empty(ModelEfficiencyMatrix.Default.SuggestModel(TaskClass.DocEdit, BudgetPressure.Tight,
            [Cli.Codex, Cli.ClaudeCode], At, Requirement with { MinimumOverall = 0, AllowProvisional = true }));
    }

    [Fact]
    public void Text_priors_cover_four_kinds_and_both_languages_without_changing_existing_policy_routes()
    {
        var catalog = TaskClassRecommendationCatalog.Default;
        Assert.Equal(8, catalog.LanguagePriors.Count);
        foreach (var kind in Enum.GetValues<TextWorkKind>())
            foreach (var language in new[] { "de", "en" })
            {
                var prior = catalog.RecommendLanguage(kind, language)!;
                Assert.Equal(TaskClass.DocEdit, prior.TaskClass);
                Assert.Equal(language, prior.Requirement.Language);
                Assert.Equal(.75m, prior.Requirement.MinimumOverall);
                Assert.False(prior.Requirement.AllowProvisional);
                Assert.Empty(prior.Candidates);
            }
        Assert.Equal("sol-xhigh", catalog.Recommend(TaskClass.SecurityAssessment).Recommended.RouteId);
    }
}
