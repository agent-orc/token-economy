using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class OutcomeEfficiencyTests
{
    private static readonly DateTime At = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly EfficiencyScope Scope = new()
    {
        TaskClass = "source-code-review",
        AcceptanceDefinition = "seeded-defect gate",
    };

    [Fact]
    public void ComputeObserved_counts_failed_retry_spend_and_uses_dated_prices()
    {
        var report = OutcomeEfficiency.ComputeObserved([
            Attempt("delivery-1", "attempt-1", 1, DeliveryOutcome.Rejected, AttemptFailureClass.Semantic, 1_000),
            Attempt("delivery-1", "attempt-2", 2, DeliveryOutcome.Accepted, AttemptFailureClass.None, 2_000),
            Attempt("delivery-2", "attempt-3", 1, DeliveryOutcome.Rejected, AttemptFailureClass.Substrate, 3_000),
        ], ModelPriceCatalog.Default, Scope);

        Assert.Equal(2, report.DeliveriesStarted);
        Assert.Equal(1, report.DeliveriesAccepted);
        Assert.Equal(6_000m, report.TokensPerAcceptedOutcome);
        Assert.Equal(3_000m, report.TokensPerStartedDelivery);
        Assert.Equal(1, report.RetryAttempts);
        Assert.Equal(.5m, report.SemanticRetryRate);
        Assert.True(report.RetryAdjustedCostPerStartedDelivery > report.InitialCostPerStartedDelivery);
        Assert.True(report.RetryCostUplift > 0);
        Assert.True(report.TokenCoverage.Complete);
        Assert.True(report.CostCoverage.Complete);
        Assert.Equal(ModelPrice.EstimatedListPricesCaveat, report.Caveat);
    }

    [Fact]
    public void Missing_usage_keeps_tokens_and_cost_unknown_not_zero()
    {
        var measured = Attempt("delivery-1", "attempt-1", 1, DeliveryOutcome.Accepted, AttemptFailureClass.None, 100);
        var report = OutcomeEfficiency.ComputeObserved([
            measured,
            measured with { DeliveryId = "delivery-2", AttemptId = "attempt-2", Usage = null },
        ], ModelPriceCatalog.Default, Scope);

        Assert.Null(report.TokensPerAcceptedOutcome);
        Assert.Null(report.CostPerAcceptedDelivery);
        Assert.Equal(100, report.KnownPartialCost > 0 ? 100 : 0);
        Assert.False(report.TokenCoverage.Complete);
        Assert.False(report.CostCoverage.Complete);
    }

    [Fact]
    public void Conflicting_replay_and_non_contiguous_ordinals_are_rejected()
    {
        var first = Attempt("delivery-1", "attempt-1", 1, DeliveryOutcome.Rejected, AttemptFailureClass.Semantic, 10);
        Assert.Throws<ArgumentException>(() => OutcomeEfficiency.ComputeObserved(
            [first, first with { Usage = new(20, 0) }], ModelPriceCatalog.Default, Scope));
        Assert.Throws<ArgumentException>(() => OutcomeEfficiency.ComputeObserved(
            [first with { AttemptOrdinal = 2 }], ModelPriceCatalog.Default, Scope));
    }

    private static DeliveryAttemptEfficiencyInput Attempt(
        string delivery, string attempt, int ordinal, DeliveryOutcome outcome,
        AttemptFailureClass failure, long input) => new()
        {
            DeliveryId = delivery,
            AttemptId = attempt,
            AttemptOrdinal = ordinal,
            Model = KnownModels.Gpt56Sol,
            ExecutedAtUtc = At,
            Usage = new(input, 0),
            Outcome = outcome,
            FailureClass = failure,
        };
}
