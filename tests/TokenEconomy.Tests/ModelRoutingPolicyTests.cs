using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelRoutingPolicyTests
{
    private static readonly ModelRoutingPolicy Policy = ModelRoutingPolicy.Default;

    [Theory]
    [InlineData(0, "luna-medium")]
    [InlineData(20, "luna-medium")]
    [InlineData(21, "terra-medium")]
    [InlineData(50, "terra-medium")]
    [InlineData(51, "sol-medium")]
    [InlineData(69, "sol-medium")]
    [InlineData(70, "sol-xhigh")]
    [InlineData(100, "sol-xhigh")]
    public void CoreScoreBands_MatchTheAuthoritativeLadder(int score, string routeId)
        => Assert.Equal(routeId, Policy.RecommendCore(new() { Scorecard = Score(score) }).Route.Id);

    [Theory]
    [InlineData("p0")]
    [InlineData("fencing")]
    [InlineData("leaseOwnership")]
    [InlineData("staleWriteRejection")]
    [InlineData("distributedAuthority")]
    [InlineData("securityBoundary")]
    [InlineData("credibleDataLoss")]
    public void CorrectnessCriticalTriggers_AlwaysRequireSolXHigh(string trigger)
    {
        foreach (var quotaPoints in new[] { 0, 5 })
        {
            var decision = Policy.RecommendCore(new()
            {
                Scorecard = Score(quotaPoints, quotaPoints),
                CorrectnessTriggers = [trigger],
            });
            Assert.Equal("sol-xhigh", decision.Route.Id);
            Assert.Contains("correctnessCritical", decision.AppliedHardFloors);
        }
    }

    [Theory]
    [InlineData("publicProtocol", "sol-medium")]
    [InlineData("persistentStateMigration", "sol-medium")]
    [InlineData("threeOrMoreRuntimeSubsystems", "sol-medium")]
    [InlineData("unclearBug", "terra-medium")]
    public void OtherHardFloors_RaiseLowScores(string trigger, string routeId)
        => Assert.Equal(routeId, Policy.RecommendCore(new() { Scorecard = Score(0), CorrectnessTriggers = [trigger] }).Route.Id);

    [Fact]
    public void BoundedDecision_UsesMiniUnlessConsequentialEvidenceIsAmbiguousOrUnbounded()
    {
        Assert.Equal("mini-high", Policy.RecommendBoundedDecision(new()).Route.Id);
        Assert.Equal("mini-high", Policy.RecommendBoundedDecision(new() { AuthorizingTriggers = ["destructiveAction"] }).Route.Id);
        Assert.Equal("sol-medium", Policy.RecommendBoundedDecision(new()
        {
            EvidenceIsAmbiguous = true,
            AuthorizingTriggers = ["destructiveAction"],
        }).Route.Id);
    }

    [Fact]
    public void SemanticReissue_SetsEmpiricalConfidenceAndPromotesAtLeastOneTier()
    {
        var decision = Policy.RecommendCore(new()
        {
            Scorecard = Score(45),
            PreviousOutcome = RoutingAttemptOutcome.SemanticFailure,
        });

        Assert.Equal(55, decision.Score);
        Assert.Equal(10, decision.EffectiveEmpiricalConfidence);
        Assert.Equal("sol-medium", decision.Route.Id);
        Assert.True(decision.ReissuePromoted);
    }

    [Theory]
    [InlineData(RoutingAttemptOutcome.EnvironmentalFailure)]
    [InlineData(RoutingAttemptOutcome.StaleBase)]
    [InlineData(RoutingAttemptOutcome.BrokenTestHost)]
    [InlineData(RoutingAttemptOutcome.Cancellation)]
    [InlineData(RoutingAttemptOutcome.QuotaTruncation)]
    [InlineData(RoutingAttemptOutcome.MissingDeliveryPath)]
    public void SubstrateFailures_DoNotPromote(RoutingAttemptOutcome outcome)
    {
        var decision = Policy.RecommendCore(new() { Scorecard = Score(45), PreviousOutcome = outcome });
        Assert.Equal("terra-medium", decision.Route.Id);
        Assert.False(decision.ReissuePromoted);
    }

    [Fact]
    public void TwoSemanticFailuresAtStrongerTier_StopEscalation()
    {
        var decision = Policy.RecommendCore(new()
        {
            Scorecard = Score(60),
            PreviousOutcome = RoutingAttemptOutcome.SemanticFailure,
            SemanticFailuresAtStrongerTier = 2,
        });

        Assert.True(decision.RequiresHumanDecision);
        Assert.Contains("escalation stops", decision.Reason);
    }

    [Fact]
    public void ScorecardRejectsPointsOutsidePolicyWeights()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Policy.RecommendCore(new()
        {
            Scorecard = new()
            {
                CorrectnessRisk = 36,
                ExpectedScope = 0,
                ContextDemand = 0,
                TaskTypeAndUncertainty = 0,
                EmpiricalConfidence = 0,
                QuotaAndCostHeadroom = 0,
            },
        }));

    [Fact]
    public void UnknownCorrectnessTrigger_IsRejectedInsteadOfIgnored()
        => Assert.Throws<ArgumentException>(() => Policy.RecommendCore(new()
        {
            Scorecard = Score(0),
            CorrectnessTriggers = ["securityBoundry"],
        }));

    [Fact]
    public void CorrectnessEvaluator_HasNoPriceCatalogOrBudgetPressureInput()
    {
        var recommend = typeof(ModelRoutingPolicy).GetMethod(nameof(ModelRoutingPolicy.RecommendCore))!;
        Assert.Equal([typeof(ModelRoutingRequest)], recommend.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(ModelRoutingPolicy).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(ModelPriceCatalog));
    }

    private static ModelRoutingScorecard Score(int total, int? quotaPoints = null)
    {
        var quota = quotaPoints ?? 0;
        var remaining = total - quota;
        var risk = Take(ref remaining, 35);
        var scope = Take(ref remaining, 20);
        var context = Take(ref remaining, 20);
        var uncertainty = Take(ref remaining, 10);
        var empirical = Take(ref remaining, 10);
        if (quotaPoints is null) quota = Take(ref remaining, 5);
        Assert.Equal(0, remaining);
        return new()
        {
            CorrectnessRisk = risk,
            ExpectedScope = scope,
            ContextDemand = context,
            TaskTypeAndUncertainty = uncertainty,
            EmpiricalConfidence = empirical,
            QuotaAndCostHeadroom = quota,
        };
    }

    private static int Take(ref int remaining, int maximum)
    {
        var value = Math.Min(remaining, maximum);
        remaining -= value;
        return value;
    }
}
