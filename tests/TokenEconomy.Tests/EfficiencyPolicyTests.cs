using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class EfficiencyPolicyTests
{
    // ---- suitability grid ----

    [Theory]
    [InlineData(CapabilityTier.Frontier, TaskClass.HeavyDesign, Suitability.Ideal)]
    [InlineData(CapabilityTier.Frontier, TaskClass.Feature, Suitability.Capable)]
    [InlineData(CapabilityTier.Frontier, TaskClass.MechanicalChore, Suitability.Overkill)]
    [InlineData(CapabilityTier.Frontier, TaskClass.DocEdit, Suitability.Overkill)]
    [InlineData(CapabilityTier.Frontier, TaskClass.Research, Suitability.Capable)]
    [InlineData(CapabilityTier.Balanced, TaskClass.HeavyDesign, Suitability.Capable)]
    [InlineData(CapabilityTier.Balanced, TaskClass.Feature, Suitability.Ideal)]
    [InlineData(CapabilityTier.Balanced, TaskClass.Research, Suitability.Ideal)]
    [InlineData(CapabilityTier.Balanced, TaskClass.MechanicalChore, Suitability.Capable)]
    [InlineData(CapabilityTier.Light, TaskClass.MechanicalChore, Suitability.Ideal)]
    [InlineData(CapabilityTier.Light, TaskClass.DocEdit, Suitability.Ideal)]
    [InlineData(CapabilityTier.Light, TaskClass.HeavyDesign, Suitability.Underpowered)]
    [InlineData(CapabilityTier.Light, TaskClass.Feature, Suitability.Underpowered)]
    [InlineData(CapabilityTier.Light, TaskClass.Research, Suitability.Underpowered)]
    public void SuitabilityFor_MapsTierAndTask(CapabilityTier tier, TaskClass task, Suitability expected)
        => Assert.Equal(expected, EfficiencyPolicy.SuitabilityFor(tier, task));

    [Fact]
    public void SuitabilityFor_CoversEveryTierAndTask()
    {
        // No pair falls through to a surprising default — every cell of the matrix is defined.
        foreach (var tier in Enum.GetValues<CapabilityTier>())
            foreach (var task in Enum.GetValues<TaskClass>())
                Assert.True(Enum.IsDefined(EfficiencyPolicy.SuitabilityFor(tier, task)));
    }

    // ---- cost-class buckets (derived, not duplicated numbers) ----

    [Fact]
    public void ClassifyCost_NullIsUnknown_NeverAGuessedBand()
        => Assert.Equal(CostClass.Unknown, EfficiencyPolicy.ClassifyCost(null));

    [Theory]
    [InlineData(0.0, CostClass.Economy)]
    [InlineData(2.00, CostClass.Economy)]    // Haiku-tier reference workload
    [InlineData(3.99, CostClass.Economy)]
    [InlineData(4.00, CostClass.Standard)]   // boundary is Standard
    [InlineData(6.00, CostClass.Standard)]   // Sonnet-tier reference workload
    [InlineData(7.99, CostClass.Standard)]
    [InlineData(8.00, CostClass.Premium)]    // boundary is Premium
    [InlineData(10.00, CostClass.Premium)]   // Opus-tier reference workload
    [InlineData(20.00, CostClass.Premium)]   // Fable-tier reference workload
    public void ClassifyCost_BucketsByReferenceTotal(double total, CostClass expected)
        => Assert.Equal(expected, EfficiencyPolicy.ClassifyCost((decimal)total));

    [Fact]
    public void CostReferenceUsage_IsInputHeavyWithModestOutput()
    {
        Assert.Equal(1_000_000, EfficiencyPolicy.CostReferenceUsage.Input);
        Assert.Equal(200_000, EfficiencyPolicy.CostReferenceUsage.Output);
    }

    // ---- suggested effort ----

    [Theory]
    [InlineData(TaskClass.HeavyDesign, EffortLevel.High)]
    [InlineData(TaskClass.Feature, EffortLevel.Medium)]
    [InlineData(TaskClass.Research, EffortLevel.Medium)]
    [InlineData(TaskClass.MechanicalChore, EffortLevel.Low)]
    [InlineData(TaskClass.DocEdit, EffortLevel.Low)]
    public void BaseEffort_MapsTask(TaskClass task, EffortLevel expected)
        => Assert.Equal(expected, EfficiencyPolicy.BaseEffort(task));

    [Theory]
    // Comfortable / Tight keep the task's base effort.
    [InlineData(TaskClass.HeavyDesign, BudgetPressure.Comfortable, EffortLevel.High)]
    [InlineData(TaskClass.HeavyDesign, BudgetPressure.Tight, EffortLevel.High)]
    // Critical steps effort down one notch...
    [InlineData(TaskClass.HeavyDesign, BudgetPressure.Critical, EffortLevel.Medium)]
    [InlineData(TaskClass.Feature, BudgetPressure.Critical, EffortLevel.Low)]
    // ...but never below Low.
    [InlineData(TaskClass.DocEdit, BudgetPressure.Critical, EffortLevel.Low)]
    [InlineData(TaskClass.MechanicalChore, BudgetPressure.Critical, EffortLevel.Low)]
    public void SuggestedEffort_StepsDownOnlyUnderCritical(TaskClass task, BudgetPressure pressure, EffortLevel expected)
        => Assert.Equal(expected, EfficiencyPolicy.SuggestedEffort(task, pressure));
}
