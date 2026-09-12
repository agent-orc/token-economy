using TokenEconomy;

// An illustrative AGT intake, saved before the first implementation run.
var card = new ComplexityCard
{
    TaskKey = "DEMO-42",
    Prompt = "Add a saved provider filter to the task list.",
    Project = "Agent Studio",
    Area = "task-list",
    TaskType = "feature",
    AcceptanceCriteria = ["The chosen provider survives reload.",
                          "Reset restores the unfiltered list."],
    ReferencedFiles = ["frontend/task-list.ts", "frontend/filter-store.ts"],
    ReferencedSubsystems = ["task-list", "filter-state"],
    ExpectedChangedLines = 120,
    RoutingSignals = new ComplexityRoutingSignals
    {
        CorrectnessRisk = 12,      // Reversible behavior; acceptance checks exist.
        ExpectedScope = 8,         // 51-200 expected lines / two components.
        ContextDemand = 8,         // One adjacent state contract to read.
        TaskUncertainty = 6,       // A specified feature.
        QuotaAndCostHeadroom = 5   // Current run has comfortable headroom.
    }
};

var estimate = new TaskComplexityEstimator().Estimate(card);
Console.WriteLine($"{estimate.Score}/100 -> {estimate.Level}");
Console.WriteLine($"Confidence: {estimate.Confidence:F2}");
Console.WriteLine(estimate.ScoreEvidence);
foreach (var dimension in estimate.Dimensions)
    Console.WriteLine($"{dimension.Name}: {dimension.Evidence}");
// 49/100 -> Standard
// Confidence: 0.55 (the no-history cap; not a success probability)

var estimates = new InMemoryTaskComplexityEstimateStore();
estimates.Upsert(estimate); // Replace with durable host storage in production.
