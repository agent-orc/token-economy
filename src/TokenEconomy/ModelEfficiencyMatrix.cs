namespace TokenEconomy;

/// <summary>
/// The token-efficiency matrix: one <see cref="ModelEfficiencyProfile"/> per catalog model, joined to a
/// <see cref="ModelPriceCatalog"/> so cost is <i>derived</i> (never duplicated), plus the
/// <see cref="SuggestModel"/> selection API. Pure and deterministic — no logging or I/O; the same inputs
/// always yield the same ranking.
/// </summary>
/// <remarks>
/// <para>
/// This is the Selection axis of token-budget load management: "was kriege ich für meine Tokens?" It
/// ranks the models available <i>right now</i> for a task under the current budget pressure, each with a
/// rationale string for the orchestrator's decision event and the Lastverteilung view.
/// </para>
/// <para>
/// The matrix supplies the ranked menu; the <i>policy</i> of when to act on it (downshift, throttle,
/// wait) stays in the admission algorithm. Use <see cref="Default"/> for the seeded matrix, or build one
/// from your own catalog + profiles.
/// </para>
/// </remarks>
public sealed class ModelEfficiencyMatrix
{
    private readonly ModelPriceCatalog _catalog;
    private readonly List<ModelEfficiencyProfile> _profiles;
    private readonly Dictionary<string, ModelEfficiencyProfile> _byId;
    private readonly Dictionary<string, int> _order;   // canonical id -> declaration index (stable final tiebreak)

    /// <summary>Build a matrix over a pricing catalog from a set of profiles.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    /// <exception cref="ArgumentException">A profile has a blank id or no effort levels, its model id is not in the catalog, or two profiles resolve to the same catalog model.</exception>
    public ModelEfficiencyMatrix(ModelPriceCatalog catalog, IEnumerable<ModelEfficiencyProfile> profiles)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _profiles = [.. profiles];
        _byId = new Dictionary<string, ModelEfficiencyProfile>(StringComparer.Ordinal);
        _order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _profiles.Count; i++)
        {
            var profile = _profiles[i];
            if (string.IsNullOrWhiteSpace(profile.ModelId))
                throw new ArgumentException("A ModelEfficiencyProfile must have a non-empty ModelId.", nameof(profiles));
            if (profile.EffortLevels.Count == 0)
                throw new ArgumentException($"Model '{profile.ModelId}' must support at least one effort level.", nameof(profiles));

            var listing = catalog.Find(profile.ModelId)
                ?? throw new ArgumentException($"Model '{profile.ModelId}' has an efficiency profile but is not in the pricing catalog.", nameof(profiles));
            if (_byId.ContainsKey(listing.ModelId))
                throw new ArgumentException($"Duplicate efficiency profile for model '{listing.ModelId}'.", nameof(profiles));

            _byId[listing.ModelId] = profile;
            _order[listing.ModelId] = i;
        }
    }

    /// <summary>Every profile, in declaration order. Declaration order doubles as the curator's tiebreak preference.</summary>
    public IReadOnlyList<ModelEfficiencyProfile> Profiles => _profiles;

    /// <summary>The pricing catalog this matrix derives cost from.</summary>
    public ModelPriceCatalog Catalog => _catalog;

    /// <summary>The default matrix: the seeded profiles over <see cref="ModelPriceCatalog.Default"/>.</summary>
    public static ModelEfficiencyMatrix Default { get; } = new(ModelPriceCatalog.Default, ModelEfficiencySeed.Profiles());

    /// <summary>The profile for a model id or alias, or null if the model has no profile. Case- and dot/dash-insensitive (via the catalog).</summary>
    public ModelEfficiencyProfile? Find(string? model)
    {
        var listing = _catalog.Find(model);
        return listing is not null && _byId.TryGetValue(listing.ModelId, out var profile) ? profile : null;
    }

    /// <summary>The CLI that runs a model given its catalog vendor, or null when the vendor maps to no known CLI.</summary>
    public static Cli? CliForVendor(string? vendor) => vendor?.Trim().ToLowerInvariant() switch
    {
        "anthropic" => Cli.Claude,
        "openai" => Cli.Codex,
        _ => null,
    };

    /// <summary>The CLI that runs <paramref name="model"/>, or null when the model is unknown or its vendor maps to no CLI.</summary>
    public Cli? CliOf(string? model) => CliForVendor(_catalog.Find(model)?.Vendor);

    /// <summary>
    /// The <see cref="CostClass"/> for <paramref name="model"/> at <paramref name="atUtc"/>, derived by
    /// costing <see cref="EfficiencyPolicy.CostReferenceUsage"/> through the pricing catalog. An unknown
    /// or unpriced model returns <see cref="CostClass.Unknown"/>.
    /// </summary>
    public CostClass CostClassOf(string? model, DateTime atUtc)
        => EfficiencyPolicy.ClassifyCost(_catalog.ComputeCost(model, EfficiencyPolicy.CostReferenceUsage, atUtc).Total);

    /// <summary>The suitability of <paramref name="model"/> for <paramref name="taskClass"/>, or null when the model has no profile.</summary>
    public Suitability? SuitabilityOf(string? model, TaskClass taskClass)
    {
        var profile = Find(model);
        return profile is null ? null : EfficiencyPolicy.SuitabilityFor(profile.Tier, taskClass);
    }

    /// <summary>
    /// The full matrix as inspectable rows at <paramref name="atUtc"/> — one <see cref="ModelEfficiencyRow"/>
    /// per profile with tier, derived cost class, effort levels and suitability for every task class.
    /// Includes restricted and deprecated models (flagged); render this to show the matrix itself.
    /// </summary>
    public IReadOnlyList<ModelEfficiencyRow> Describe(DateTime atUtc)
    {
        var rows = new List<ModelEfficiencyRow>(_profiles.Count);
        foreach (var profile in _profiles)
        {
            var listing = _catalog.Find(profile.ModelId)!;   // validated in the constructor
            var breakdown = _catalog.ComputeCost(listing.ModelId, EfficiencyPolicy.CostReferenceUsage, atUtc);
            var suitability = new Dictionary<TaskClass, Suitability>();
            foreach (var taskClass in Enum.GetValues<TaskClass>())
                suitability[taskClass] = EfficiencyPolicy.SuitabilityFor(profile.Tier, taskClass);

            rows.Add(new ModelEfficiencyRow
            {
                ModelId = listing.ModelId,
                Vendor = listing.Vendor,
                Cli = CliForVendor(listing.Vendor),
                Tier = profile.Tier,
                CostClass = EfficiencyPolicy.ClassifyCost(breakdown.Total),
                EffortLevels = profile.EffortLevels,
                Suitability = suitability,
                Restricted = profile.Restricted,
                Deprecated = profile.Deprecated,
                CostUnconfirmed = breakdown.Unconfirmed,
                Note = profile.Note,
            });
        }
        return rows;
    }

    /// <summary>
    /// Rank the models selectable right now for <paramref name="taskClass"/> under
    /// <paramref name="budgetPressure"/>, restricted to the CLIs in <paramref name="availableClis"/>.
    /// Best candidate first; each carries a <see cref="ModelSuggestion.Score"/>, a suggested effort and a
    /// <see cref="ModelSuggestion.Rationale"/>. Restricted and deprecated models are never suggested.
    /// </summary>
    /// <param name="taskClass">The class of work the run performs.</param>
    /// <param name="budgetPressure">How hard the remaining budget constrains the choice.</param>
    /// <param name="availableClis">The CLIs with budget/quota right now; a model is a candidate only if its CLI is in this set. Null is treated as none.</param>
    /// <param name="atUtc">The instant whose prices drive the cost classes (prices have history).</param>
    /// <returns>
    /// The qualifying candidates, best-first. An <b>empty</b> list means no model from the available CLIs
    /// qualifies — the caller should wait (with a visible reason) rather than launch, per the control loop.
    /// </returns>
    public IReadOnlyList<ModelSuggestion> SuggestModel(
        TaskClass taskClass,
        BudgetPressure budgetPressure,
        IEnumerable<Cli>? availableClis,
        DateTime atUtc)
    {
        var available = new HashSet<Cli>();
        if (availableClis is not null)
            available.UnionWith(availableClis);
        var desiredEffort = EfficiencyPolicy.SuggestedEffort(taskClass, budgetPressure);

        var candidates = new List<ModelSuggestion>();
        foreach (var profile in _profiles)
        {
            if (profile.Restricted || profile.Deprecated)
                continue;

            var listing = _catalog.Find(profile.ModelId)!;   // validated in the constructor
            var cli = CliForVendor(listing.Vendor);
            if (cli is null || !available.Contains(cli.Value))
                continue;

            var suitability = EfficiencyPolicy.SuitabilityFor(profile.Tier, taskClass);
            var breakdown = _catalog.ComputeCost(listing.ModelId, EfficiencyPolicy.CostReferenceUsage, atUtc);
            var cost = EfficiencyPolicy.ClassifyCost(breakdown.Total);
            var effort = ClampEffort(desiredEffort, profile.EffortLevels);

            candidates.Add(new ModelSuggestion
            {
                ModelId = listing.ModelId,
                Cli = cli.Value,
                Tier = profile.Tier,
                CostClass = cost,
                Suitability = suitability,
                SuggestedEffort = effort,
                Score = EfficiencyPolicy.Score(suitability, cost, budgetPressure),
                Rationale = BuildRationale(listing.ModelId, taskClass, budgetPressure, profile.Tier, suitability, cost, effort),
                CostUnconfirmed = breakdown.Unconfirmed,
            });
        }

        candidates.Sort(Compare);
        return candidates;
    }

    /// <summary>Total order over candidates: score, then capability fit, then known-cost, then cheaper-cost, then declaration order.</summary>
    private int Compare(ModelSuggestion a, ModelSuggestion b)
    {
        var byScore = b.Score.CompareTo(a.Score);                    // higher score first
        if (byScore != 0) return byScore;

        var bySuit = b.Suitability.CompareTo(a.Suitability);         // better capability fit first
        if (bySuit != 0) return bySuit;

        var aUnknown = a.CostClass == CostClass.Unknown ? 1 : 0;     // known cost before unprojectable cost
        var bUnknown = b.CostClass == CostClass.Unknown ? 1 : 0;
        if (aUnknown != bUnknown) return aUnknown - bUnknown;

        var byCost = a.CostClass.CompareTo(b.CostClass);             // cheaper cost class first
        if (byCost != 0) return byCost;

        return _order[a.ModelId].CompareTo(_order[b.ModelId]);       // stable: curator's declaration order
    }

    /// <summary>Clamp a desired effort into the range a model actually supports.</summary>
    private static EffortLevel ClampEffort(EffortLevel desired, IReadOnlyList<EffortLevel> supported)
    {
        var min = EffortLevel.High;
        var max = EffortLevel.Low;
        foreach (var e in supported)
        {
            if (e < min) min = e;
            if (e > max) max = e;
        }
        if (desired < min) return min;
        if (desired > max) return max;
        return desired;
    }

    private static string BuildRationale(
        string modelId, TaskClass taskClass, BudgetPressure pressure,
        CapabilityTier tier, Suitability suitability, CostClass cost, EffortLevel effort)
    {
        var task = TaskLabel(taskClass);
        var fit = suitability switch
        {
            Suitability.Ideal => $"an ideal match for {task}",
            Suitability.Capable => $"a capable choice for {task}",
            Suitability.Overkill => $"more capability than {task} needs",
            Suitability.Underpowered => $"likely underpowered for {task}",
            _ => $"a candidate for {task}",
        };
        var costWord = cost switch
        {
            CostClass.Economy => "economy cost",
            CostClass.Standard => "standard cost",
            CostClass.Premium => "premium cost",
            CostClass.Unknown => "unpriced",
            _ => "unknown cost",
        };
        var tierWord = tier.ToString().ToLowerInvariant();
        var effortWord = effort.ToString().ToLowerInvariant();
        return $"{modelId}: {tierWord} tier, {fit}; {costWord}{PressureClause(pressure, cost)}. Suggested effort: {effortWord}.";
    }

    /// <summary>The clause explaining how budget pressure weighed on this cost class (empty when budget is comfortable).</summary>
    private static string PressureClause(BudgetPressure pressure, CostClass cost)
    {
        if (pressure == BudgetPressure.Comfortable)
            return "";
        var word = pressure == BudgetPressure.Critical ? "critical" : "tight";
        return cost switch
        {
            CostClass.Economy => $" — favoured to conserve budget under {word} pressure",
            CostClass.Standard => $" — moderate spend under {word} pressure",
            CostClass.Premium => $" — penalised by {word} budget pressure",
            CostClass.Unknown => $" — deprioritised (cost not projectable) under {word} pressure",
            _ => $" under {word} pressure",
        };
    }

    private static string TaskLabel(TaskClass taskClass) => taskClass switch
    {
        TaskClass.HeavyDesign => "heavy-design work",
        TaskClass.Feature => "feature work",
        TaskClass.MechanicalChore => "mechanical-chore work",
        TaskClass.DocEdit => "doc-edit work",
        TaskClass.Research => "research work",
        _ => "this work",
    };
}
