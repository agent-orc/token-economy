namespace TokenEconomy;

/// <summary>
/// The pure, data-only policies the token-efficiency matrix ranks with: the capability×task suitability
/// grid, the cost-class buckets, the scoring weights, and the suggested-effort rules. Everything here is
/// deterministic and side-effect free.
/// </summary>
/// <remarks>
/// This is the Selection axis's <i>knowledge</i>, not its <i>policy</i>: it answers "how well does this
/// tier fit this task, and how does cost weigh under this pressure?" The decision of when to actually
/// downshift / throttle / wait on a ranking stays in the admission algorithm, by design.
/// </remarks>
public static class EfficiencyPolicy
{
    // ---- suitability grid: capability tier × task class ----

    /// <summary>How well a capability <paramref name="tier"/> fits a <paramref name="taskClass"/>.</summary>
    public static Suitability SuitabilityFor(CapabilityTier tier, TaskClass taskClass) => tier switch
    {
        // Frontier: made for the hard stuff, wasteful on the easy stuff.
        CapabilityTier.Frontier => taskClass switch
        {
            TaskClass.HeavyDesign => Suitability.Ideal,
            TaskClass.Feature => Suitability.Capable,
            TaskClass.Research => Suitability.Capable,
            TaskClass.MechanicalChore => Suitability.Overkill,
            TaskClass.DocEdit => Suitability.Overkill,
            _ => Suitability.Capable,
        },
        // Balanced: the everyday sweet spot; can stretch to design, fine on chores.
        CapabilityTier.Balanced => taskClass switch
        {
            TaskClass.HeavyDesign => Suitability.Capable,
            TaskClass.Feature => Suitability.Ideal,
            TaskClass.Research => Suitability.Ideal,
            TaskClass.MechanicalChore => Suitability.Capable,
            TaskClass.DocEdit => Suitability.Capable,
            _ => Suitability.Capable,
        },
        // Light: perfect for rote and prose, out of its depth on reasoning-heavy work.
        CapabilityTier.Light => taskClass switch
        {
            TaskClass.MechanicalChore => Suitability.Ideal,
            TaskClass.DocEdit => Suitability.Ideal,
            TaskClass.HeavyDesign => Suitability.Underpowered,
            TaskClass.Feature => Suitability.Underpowered,
            TaskClass.Research => Suitability.Underpowered,
            _ => Suitability.Underpowered,
        },
        _ => Suitability.Underpowered,
    };

    // ---- scoring: capability fit dominates, cost tips the balance under pressure ----

    /// <summary>
    /// Base score for a suitability rating. The gaps are wide enough that even the strongest cost
    /// preference (an <see cref="CostClass.Economy"/> model under <see cref="BudgetPressure.Critical"/>)
    /// cannot lift an <see cref="Suitability.Underpowered"/> model above a <see cref="Suitability.Capable"/> one.
    /// </summary>
    internal static int SuitabilityScore(Suitability s) => s switch
    {
        Suitability.Ideal => 100,
        Suitability.Capable => 60,
        Suitability.Overkill => 30,
        Suitability.Underpowered => -40,
        _ => -40,
    };

    /// <summary>How strongly cost sways the ranking at a given pressure — the magnitude added/subtracted per cost class.</summary>
    internal static int CostWeight(BudgetPressure pressure) => pressure switch
    {
        BudgetPressure.Comfortable => 5,   // barely a tiebreak; capability leads
        BudgetPressure.Tight => 20,
        BudgetPressure.Critical => 40,     // never enough to beat a full suitability gap (100→60)
        _ => 5,
    };

    /// <summary>The cost adjustment for a class at a pressure: cheaper models gain, pricier lose, unknown-cost is treated as unfavourable.</summary>
    internal static int CostAdjustment(CostClass cost, BudgetPressure pressure)
    {
        var w = CostWeight(pressure);
        return cost switch
        {
            CostClass.Economy => +w,
            CostClass.Standard => 0,
            CostClass.Premium => -w,
            // Unknown spend cannot be projected: a mild caveat when budget is comfortable, but treated
            // as unfavourably as premium once there is any pressure — you should not route a
            // budget-pressured run onto a model whose cost you cannot bound.
            CostClass.Unknown => pressure == BudgetPressure.Comfortable ? -5 : -w,
            _ => 0,
        };
    }

    /// <summary>The full ranking score for a candidate: capability fit adjusted by cost under the given pressure.</summary>
    internal static int Score(Suitability suitability, CostClass cost, BudgetPressure pressure)
        => SuitabilityScore(suitability) + CostAdjustment(cost, pressure);

    // ---- cost-class derivation (uses the pricing catalog, never a duplicated number) ----

    /// <summary>
    /// A fixed, representative coding-agent workload used only to <i>bucket</i> models into cost classes
    /// by running it through the pricing catalog. It is not a billing figure: the same mix is applied to
    /// every model, so the resulting order reflects relative price. (Input-heavy with modest output, as a
    /// typical agent turn is.)
    /// </summary>
    public static readonly TokenUsage CostReferenceUsage = new(Input: 1_000_000, Output: 200_000);

    /// <summary>
    /// Bucket the cost of <see cref="CostReferenceUsage"/> into a <see cref="CostClass"/>. A null cost
    /// (the model is unknown or unpriced at the requested time) maps to <see cref="CostClass.Unknown"/> —
    /// never a guessed band. Thresholds are coarse and sit between the seeded tiers with margin.
    /// </summary>
    public static CostClass ClassifyCost(decimal? referenceTotal) => referenceTotal switch
    {
        null => CostClass.Unknown,
        < 4m => CostClass.Economy,     // e.g. a Haiku-tier reference workload (~$2)
        < 8m => CostClass.Standard,    // e.g. a Sonnet-tier reference workload (~$6)
        _ => CostClass.Premium,        // e.g. an Opus-/Fable-tier reference workload (≥$10)
    };

    // ---- suggested effort: from the task, then trimmed under budget pressure ----

    /// <summary>The reasoning effort a task class wants before any budget adjustment.</summary>
    public static EffortLevel BaseEffort(TaskClass taskClass) => taskClass switch
    {
        TaskClass.HeavyDesign => EffortLevel.High,
        TaskClass.Feature => EffortLevel.Medium,
        TaskClass.Research => EffortLevel.Medium,
        TaskClass.MechanicalChore => EffortLevel.Low,
        TaskClass.DocEdit => EffortLevel.Low,
        _ => EffortLevel.Medium,
    };

    /// <summary>
    /// The effort to request for a task under budget pressure: <see cref="BudgetPressure.Critical"/>
    /// steps the effort down one notch (never below <see cref="EffortLevel.Low"/>) to conserve tokens;
    /// lighter pressure keeps the task's base effort. The result is still clamped per model to the levels
    /// it actually supports.
    /// </summary>
    public static EffortLevel SuggestedEffort(TaskClass taskClass, BudgetPressure pressure)
    {
        var effort = BaseEffort(taskClass);
        if (pressure == BudgetPressure.Critical && effort > EffortLevel.Low)
            effort -= 1;
        return effort;
    }
}
