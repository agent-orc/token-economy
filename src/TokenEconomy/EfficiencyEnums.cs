namespace TokenEconomy;

/// <summary>
/// The class of work a run performs, used to pick the model whose capability best fits the task
/// without overpaying. These are the coarse buckets the admission algorithm tags a card with before
/// asking <see cref="ModelEfficiencyMatrix.SuggestModel"/> for a model.
/// </summary>
public enum TaskClass
{
    /// <summary>Architecture, non-trivial refactors, hard problem-solving — needs the strongest reasoning.</summary>
    HeavyDesign,

    /// <summary>Implementing a feature or a normal bug fix — the everyday coding workload.</summary>
    Feature,

    /// <summary>Rote, well-specified edits: renames, formatting, dependency bumps, boilerplate.</summary>
    MechanicalChore,

    /// <summary>Prose work: docs, comments, changelogs, release notes.</summary>
    DocEdit,

    /// <summary>Reading and analysing code or docs to answer a question or produce findings.</summary>
    Research,
}

/// <summary>
/// How much the remaining token budget constrains the choice, as decided by the admission algorithm's
/// projection (burn rate vs. remaining window). Higher pressure shifts the ranking away from expensive
/// models toward cheaper ones; it does not change a model's capability, only its desirability.
/// </summary>
public enum BudgetPressure
{
    /// <summary>Budget is ample; capability leads and cost is barely a factor.</summary>
    Comfortable,

    /// <summary>Budget is getting tight; cost meaningfully discounts pricier models.</summary>
    Tight,

    /// <summary>Near the wall; cost dominates and cheaper models are strongly preferred.</summary>
    Critical,
}

/// <summary>
/// A coarse capability band for a model, independent of price, ordered from least to most capable so
/// bands can be compared. Used with a <see cref="TaskClass"/> to rate <see cref="Suitability"/>. These
/// are maintained routing estimates, not benchmark scores.
/// </summary>
public enum CapabilityTier
{
    /// <summary>Lightweight, fast, cheapest — great for rote and prose work, weak on hard reasoning.</summary>
    Light,

    /// <summary>The balanced workhorse — the sweet spot for everyday feature work and research.</summary>
    Balanced,

    /// <summary>Top-end reasoning — for architecture and the hardest problems; overkill for simple tasks.</summary>
    Frontier,
}

/// <summary>
/// A reasoning-effort setting passed to a model. Higher effort spends more tokens on internal
/// reasoning. Ordered <see cref="Low"/> &lt; <see cref="Medium"/> &lt; <see cref="High"/> so values can
/// be compared and clamped to what a model supports.
/// </summary>
public enum EffortLevel
{
    /// <summary>Minimal reasoning — fastest and cheapest.</summary>
    Low,

    /// <summary>Moderate reasoning — the everyday default.</summary>
    Medium,

    /// <summary>Extended reasoning — most thorough and most expensive.</summary>
    High,
}

/// <summary>
/// A coarse spend band for a model, <b>derived</b> from the pricing catalog (never a duplicated
/// number): the cost of a fixed reference workload is bucketed into these classes. A model with no
/// published price resolves to <see cref="Unknown"/> rather than a guessed band. Ordered cheapest-first
/// (<see cref="Unknown"/> sorts last) so candidates can be compared by relative cost.
/// </summary>
public enum CostClass
{
    /// <summary>Cheapest band — the reference workload costs the least here.</summary>
    Economy,

    /// <summary>Mid band.</summary>
    Standard,

    /// <summary>Priciest band.</summary>
    Premium,

    /// <summary>No published price in the catalog, so the spend cannot be projected.</summary>
    Unknown,
}

/// <summary>
/// How well a model's capability fits a <see cref="TaskClass"/>. Ordered from worst fit
/// (<see cref="Underpowered"/>) to best (<see cref="Ideal"/>) so candidates can be compared by fit.
/// </summary>
public enum Suitability
{
    /// <summary>Likely to struggle — capability is below what the task needs.</summary>
    Underpowered,

    /// <summary>More capability (and cost) than the task needs; it will work but wastes budget.</summary>
    Overkill,

    /// <summary>A solid, workable choice, though not the sweet spot.</summary>
    Capable,

    /// <summary>The best-matched capability band for this task.</summary>
    Ideal,
}

/// <summary>
/// The CLI runtime that can launch a model. <see cref="ModelEfficiencyMatrix.SuggestModel"/> only ranks
/// models whose CLI is in the caller's available set, so a dry or absent CLI drops out of the ranking
/// without a launch attempt (the whole point: never learn about a dry quota by burning against it).
/// </summary>
public enum Cli
{
    /// <summary>Anthropic's Claude Code CLI (runs the <c>claude-*</c> models).</summary>
    Claude,

    /// <summary>OpenAI's Codex CLI (runs the <c>gpt-*</c> models).</summary>
    Codex,
}
