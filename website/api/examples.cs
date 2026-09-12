using System.Globalization;
using TokenEconomy;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var atUtc = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);
var prices = ModelPriceCatalog.Default;
var matrix = ModelEfficiencyMatrix.Default;

// 1. Evaluate one named model. This returns an object, or null.
ModelSuggestion? sonnet = matrix.EvaluateModel(
    KnownModels.ClaudeSonnet5, TaskClass.Feature, BudgetPressure.Tight, atUtc);
if (sonnet is not null)
{
    Console.WriteLine($"EvaluateModel: {sonnet.ModelId}; effort={sonnet.SuggestedEffort}; cli={sonnet.Cli}");
    Console.WriteLine($"Signals: tier={sonnet.Tier}; fit={sonnet.Suitability}; cost={sonnet.CostClass}; score={sonnet.Score}");
    Console.WriteLine($"Evidence: {sonnet.EvidenceStatus}; provisional={sonnet.Provisional}; unconfirmedPrice={sonnet.CostUnconfirmed}");
    Console.WriteLine(sonnet.Rationale);
}
else
{
    Console.WriteLine("No supported evaluation for this model and task class.");
}

// Optional desiredEffort overrides the default before clamping to supported levels.
ModelSuggestion? sonnetHigh = matrix.EvaluateModel(
    KnownModels.ClaudeSonnet5, TaskClass.Feature, BudgetPressure.Tight, atUtc,
    desiredEffort: EffortLevel.High);
Console.WriteLine($"Explicit desiredEffort: {sonnetHigh?.SuggestedEffort}");

// Fable 5.1 uses the same contract and retains provisional local fit.
ModelSuggestion? fable = matrix.EvaluateModel(KnownModels.ClaudeFable51, TaskClass.Feature, BudgetPressure.Tight, atUtc, desiredEffort: EffortLevel.High);
Console.WriteLine($"Fable 5.1: {fable?.ModelId} @ {fable?.SuggestedEffort}; provisional={fable?.Provisional}");
Console.WriteLine($"Fable publication: {prices.Find(KnownModels.ClaudeFable51)?.ReleaseDate:yyyy-MM-dd}");

// 2. Compare policy-selectable core models available through the supplied CLIs.
IReadOnlyList<ModelSuggestion> ranked = matrix.SuggestModel(
    TaskClass.Feature, BudgetPressure.Tight, availableClis: [Cli.Codex], atUtc: atUtc);
if (ranked.Count > 0)
    Console.WriteLine($"SuggestModel: {ranked[0].ModelId} @ {ranked[0].SuggestedEffort}; candidates={ranked.Count}");
else
    Console.WriteLine("No eligible model; retain a wait reason instead of indexing the empty list.");
Console.WriteLine($"Claude-only core candidates: {matrix.SuggestModel(TaskClass.Feature, BudgetPressure.Tight, [Cli.Claude], atUtc).Count}");

// 3. Price measured, disjoint token categories at the execution timestamp.
CostBreakdown cost = prices.ComputeCost(
    KnownModels.ClaudeSonnet5,
    new TokenUsage(Input: 100_000, Output: 10_000, CacheRead: 20_000, CacheWrite: 5_000),
    atUtc);
if (cost.HasPrice)
{
    Console.WriteLine($"ComputeCost: input={cost.InputCost:0.00000}; output={cost.OutputCost:0.00000}; cacheRead={cost.CacheReadCost:0.00000}; cacheWrite={cost.CacheWriteCost:0.00000}");
    Console.WriteLine($"Total: {cost.Total:0.00000} {cost.Currency}; caveat={cost.Caveat}");
}
else
    Console.WriteLine($"Cost unavailable: {cost.Status}");

CostBreakdown unknown = prices.ComputeCost(ModelId.Of("not-in-this-catalog"), new TokenUsage(100, 10), atUtc);
Console.WriteLine($"Unknown model: {unknown.Status}; total={(unknown.Total is null ? "null" : unknown.Total.ToString())}");

// 4. Describe returns cost bands and capability data. ResolvePrice supplies actual rates.
ModelEfficiencyRow row = matrix.Describe(atUtc).Single(row => row.ModelId == KnownModels.ClaudeSonnet5.Value);
PriceResolution price = prices.ResolvePrice(KnownModels.ClaudeSonnet5, atUtc);
Console.WriteLine($"Describe: {row.ModelId}; status={row.RoutingStatus}; efforts={string.Join(',', row.EffortLevels)}");
if (price.Found)
    Console.WriteLine($"ResolvePrice: input={price.Price!.InputPerMTok:0.00}; output={price.Price.OutputPerMTok:0.00} {price.Price.Currency}/MTok; validFrom={price.Price.ValidFrom:yyyy-MM-dd}");
else
    Console.WriteLine($"Price unavailable: {price.Status}");

// 5. Demo observations only. A real host supplies fresh provider/CLI probes and quota telemetry.
// No provider is contacted by this sample or by the library.
ProviderAvailabilitySnapshot snapshot = new ProviderQuotaDashboardBuilder().BuildSnapshot(
    records: [],
    options: new ProviderAvailabilitySnapshotOptions(
        DecisionAtUtc: atUtc,
        TrailingWindow: TimeSpan.FromHours(1),
        MaximumObservationAge: TimeSpan.FromMinutes(15),
        Providers:
        [
            new("openai", "codex", ProviderCliAvailability.Available, atUtc,
                [KnownModels.Gpt56Luna, KnownModels.Gpt56Terra, KnownModels.Gpt56Sol]),
            new("anthropic", "claude", ProviderCliAvailability.Available, atUtc,
                [KnownModels.ClaudeSonnet5]),
        ],
        QuotaWindows:
        [
            new("openai", "codex", "five-hour", UsedTokens: 10_000, LimitTokens: 100_000,
                ObservedAtUtc: atUtc, ResetsAtUtc: atUtc.AddHours(4)),
            new("anthropic", "claude", "five-hour", UsedTokens: 30_000, LimitTokens: 100_000,
                ObservedAtUtc: atUtc, ResetsAtUtc: atUtc.AddHours(4)),
        ]));

TaskClassRecommendationCatalog advice = TaskClassRecommendationCatalog.Default;
TaskClassRecommendation recommendation = advice.Recommend(TaskClass.Feature);
Console.WriteLine($"Recommend: candidates={recommendation.Candidates.Count}; status={recommendation.Status}; equivalence={recommendation.Equivalence}");
foreach (TaskClassRouteRecommendation candidate in recommendation.Candidates)
    Console.WriteLine($"Candidate: {candidate.Model} @ {candidate.ThinkingLevel}; rank={candidate.Rank}");
TaskClassSelectionResult selection = advice.Select(recommendation, snapshot, atUtc);
if (selection.Selected is { } selected)
    Console.WriteLine($"Select: {selection.Disposition}; {selected.Model} @ {selected.ThinkingLevel}");
else
    Console.WriteLine($"Select: {selection.Disposition}; {selection.Reason}");

TaskClassSelectionResult staleSelection = advice.Select(
    recommendation, snapshot with { DecisionAtUtc = atUtc.AddHours(-1) }, atUtc);
Console.WriteLine($"Stale quota: {staleSelection.Disposition}; selected={(staleSelection.Selected is null ? "null" : "present")}");

// 6. Route the concrete task before launch. Task-class advice is not admission.
ComplexityCard task = new()
{
    TaskKey = "DEMO-1",
    Prompt = "Add an optional display name to stored user preferences and migrate existing records.",
    TaskType = "feature",
    AcceptanceCriteria = ["Existing records retain their saved preferences.", "Migration tests pass."],
    ReferencedSubsystems = ["preferences"],
    HardFloorTriggers = [ComplexityHardFloorTrigger.PersistentStateMigration],
};
TaskComplexityEstimate estimate = new TaskComplexityEstimator().Estimate(task);
ModelRoutingResult result = ModelRouter.Default.Route(new ModelRoutingSelectionRequest
{
    Task = task,
    UpfrontEstimate = estimate,
    AvailableClis = [Cli.Codex, Cli.Claude],
    Capacity = new ModelRoutingCapacity
    {
        ProviderAvailability = snapshot,
        BudgetPressure = BudgetPressure.Tight,
        DeterministicVerificationAvailable = true,
    },
});
Console.WriteLine($"Estimate: score={estimate.Score}; band={estimate.Level}; confidence={estimate.Confidence:0.00}");
Console.WriteLine($"Route: {result.Disposition}; recommended={result.RecommendedRoute.ModelId} @ {result.RecommendedRoute.ThinkingLevel}; source={result.SelectionSource}");
if (result.Disposition == ModelRoutingDisposition.Selected && result.SelectedRoute is { } launch)
    Console.WriteLine($"Selected route: {launch.ModelId} @ {launch.ThinkingLevel}; CLI={launch.Cli}");
else
    Console.WriteLine($"Do not launch: {result.FallbackOrWaitReason}");
Console.WriteLine($"Hard floor: {result.CorrectnessFloor.RouteId}; applied={string.Join(',', result.CorrectnessFloor.AppliedFloorIds)}");
Console.WriteLine($"Audit: policy={result.PolicyVersion}; uncertainties={result.Uncertainty.Reasons.Count}");
