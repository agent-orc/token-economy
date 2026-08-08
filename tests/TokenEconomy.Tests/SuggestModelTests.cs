using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class SuggestModelTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ModelEfficiencyMatrix Matrix = ModelEfficiencyMatrix.Default;

    [Theory]
    [InlineData(TaskClass.HeavyDesign, "gpt-5.6-sol", EffortLevel.High)]
    [InlineData(TaskClass.Feature, "gpt-5.6-terra", EffortLevel.Medium)]
    [InlineData(TaskClass.MechanicalChore, "gpt-5.6-luna", EffortLevel.Low)]
    [InlineData(TaskClass.DocEdit, "gpt-5.6-luna", EffortLevel.Low)]
    public void CompatibilityRanking_UsesPolicyQualifiedCoreModels(TaskClass task, string expectedModel, EffortLevel expectedEffort)
    {
        var top = Matrix.SuggestModel(task, BudgetPressure.Comfortable, [Cli.Codex], Now)[0];

        Assert.Equal(expectedModel, top.ModelId);
        Assert.Equal(expectedEffort, top.SuggestedEffort);
    }

    [Fact]
    public void UnsupportedDeprecatedAndRoleExceptionModels_AreNeverCoreSuggestions()
    {
        var suggestions = Matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Comfortable, [Cli.Claude, Cli.Codex], Now);

        Assert.DoesNotContain(suggestions, candidate => candidate.ModelId.StartsWith("claude-opus", StringComparison.Ordinal));
        Assert.DoesNotContain(suggestions, candidate => candidate.ModelId == "gpt-5.4-mini");
        Assert.All(suggestions, candidate => Assert.NotEqual(PolicyEvidenceStatus.Unknown, candidate.EvidenceStatus));
    }

    [Fact]
    public void CompatibilityRanking_DoesNotInferAnEquivalentProviderFallback()
    {
        var primaryAvailable = Matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Comfortable, [Cli.Codex, Cli.Claude], Now);
        var fallbackOnly = Matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Comfortable, [Cli.Claude], Now);

        Assert.DoesNotContain(primaryAvailable, candidate => candidate.ModelId == "claude-sonnet-5");
        Assert.Empty(fallbackOnly);
        Assert.Equal("claude-sonnet-5", Assert.Single(ModelRoutingKnowledgeBase.Default.FallbacksFor("sol-medium")).ModelId);
    }

    [Fact]
    public void NoAvailableCli_YieldsEmptySoCallerWaits()
    {
        Assert.Empty(Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Comfortable, [], Now));
        Assert.Empty(Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Comfortable, null, Now));
    }

    [Fact]
    public void CriticalBudgetCannotMakeAnUnderpoweredCoreModelBeatACapableOne()
    {
        var ranked = Matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Critical, [Cli.Codex], Now);
        var luna = ranked.Single(candidate => candidate.ModelId == "gpt-5.6-luna");
        var terra = ranked.Single(candidate => candidate.ModelId == "gpt-5.6-terra");

        Assert.Equal(Suitability.Underpowered, luna.Suitability);
        Assert.True(terra.Score > luna.Score);
    }

    [Fact]
    public void EverySuggestionCarriesEvidenceAndRationale()
    {
        foreach (var suggestion in Matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Tight, [Cli.Codex], Now))
        {
            Assert.Contains(suggestion.ModelId, suggestion.Rationale);
            Assert.Contains("feature work", suggestion.Rationale);
            Assert.NotEqual(PolicyEvidenceStatus.Unknown, suggestion.EvidenceStatus);
        }
    }
}
