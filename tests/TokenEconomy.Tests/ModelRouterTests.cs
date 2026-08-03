using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelRouterTests
{
    private static readonly DateTime DecisionAt = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "luna-medium")]
    [InlineData(20, "luna-medium")]
    [InlineData(21, "terra-medium")]
    [InlineData(50, "terra-medium")]
    [InlineData(51, "sol-medium")]
    [InlineData(69, "sol-medium")]
    [InlineData(70, "sol-xhigh")]
    [InlineData(100, "sol-xhigh")]
    public void Route_AllScoreThresholdsSelectThePolicyRecommendation(int score, string expectedRoute)
    {
        var result = ModelRouter.Default.Route(Request(score));

        Assert.Equal(ModelRoutingDisposition.Selected, result.Disposition);
        Assert.Equal(expectedRoute, result.RecommendedRoute.RouteId);
        Assert.Equal(expectedRoute, result.SelectedRoute?.RouteId);
        Assert.Equal(ModelRouteSelectionSource.PolicyRecommendation, result.SelectionSource);
        AssertCompleteAudit(result, score);
    }

    [Theory]
    [InlineData(21, "luna-medium")]
    [InlineData(26, "luna-medium")]
    [InlineData(51, "terra-medium")]
    [InlineData(56, "terra-medium")]
    [InlineData(70, "sol-medium")]
    [InlineData(75, "sol-medium")]
    public void Route_QuotaDowngradeUsesOnlyTheFivePointWindow(int score, string expectedRoute)
    {
        var result = ModelRouter.Default.Route(Request(score, openAi: AvailabilityWarningState.Warning,
            deterministicVerification: true));

        Assert.Equal(ModelRoutingDisposition.Selected, result.Disposition);
        Assert.Equal(ModelRouteSelectionSource.OneTierQuotaDowngrade, result.SelectionSource);
        Assert.Equal(expectedRoute, result.SelectedRoute?.RouteId);
        Assert.Empty(result.CorrectnessFloor.AppliedFloorIds);
    }

    [Theory]
    [InlineData(27)]
    [InlineData(57)]
    [InlineData(76)]
    public void Route_QuotaDowngradeOutsideFivePointWindowWaits(int score)
    {
        var result = ModelRouter.Default.Route(Request(score, openAi: AvailabilityWarningState.Warning,
            deterministicVerification: true));

        Assert.Equal(ModelRoutingDisposition.Wait, result.Disposition);
        Assert.Equal(ModelRouteSelectionSource.WaitForSafeRoute, result.SelectionSource);
        Assert.Null(result.SelectedRoute);
    }

    [Fact]
    public void Route_EquivalentQualifiedProviderFallbackPrecedesDowngrade()
    {
        var result = ModelRouter.Default.Route(Request(51,
            openAi: AvailabilityWarningState.Warning,
            claude: AvailabilityWarningState.Healthy,
            deterministicVerification: true,
            benchmark: EvidenceFor("claude-sonnet-5", "high", RoutingQualificationLevel.BelowConfidenceGate)));

        Assert.Equal(ModelRouteSelectionSource.EquivalentProviderFallback, result.SelectionSource);
        Assert.Equal("claude-sonnet-5-high", result.SelectedRoute?.RouteId);
        Assert.Equal("claude-sonnet-5", result.SelectedRoute?.ModelId);
        Assert.Equal("high", result.SelectedRoute?.ThinkingLevel);
        Assert.Equal(RoutingQualificationLevel.BelowConfidenceGate, result.SelectedRoute?.BenchmarkQualification?.Level);
    }

    [Fact]
    public void Route_MissingFallbackQualificationDoesNotInventEquivalence()
    {
        var result = ModelRouter.Default.Route(Request(57,
            openAi: AvailabilityWarningState.Warning,
            claude: AvailabilityWarningState.Healthy,
            deterministicVerification: true,
            benchmark: EmptyEvidence()));

        Assert.Equal(ModelRoutingDisposition.Wait, result.Disposition);
        Assert.Null(result.SelectedRoute);
        Assert.Contains(result.Uncertainty.Reasons, reason => reason.Contains("lacks a usable benchmark qualification", StringComparison.Ordinal));
    }

    [Fact]
    public void Route_AmbiguousBenchmarkCapabilitiesRequireAnExplicitCapability()
    {
        var evidence = EvidenceFor("claude-sonnet-5", "high", RoutingQualificationLevel.BelowConfidenceGate);
        var cohort = Assert.Single(evidence.ObservationalCohorts);
        evidence = evidence with { ObservationalCohorts = [cohort, cohort with { Capability = "security-review" }] };

        var ambiguous = ModelRouter.Default.Route(Request(57,
            openAi: AvailabilityWarningState.Warning,
            claude: AvailabilityWarningState.Healthy,
            benchmark: evidence));
        var qualified = ModelRouter.Default.Route(Request(57,
            openAi: AvailabilityWarningState.Warning,
            claude: AvailabilityWarningState.Healthy,
            benchmark: evidence,
            requiredBenchmarkCapability: "repository-editing"));

        Assert.Equal(ModelRoutingDisposition.Wait, ambiguous.Disposition);
        Assert.Equal(ModelRouteSelectionSource.EquivalentProviderFallback, qualified.SelectionSource);
    }

    [Theory]
    [InlineData(ComplexityHardFloorTrigger.P0, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.Fencing, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.LeaseOwnership, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.StaleWriteRejection, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.DistributedAuthority, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.SecurityBoundary, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.CredibleDataLoss, "sol-xhigh")]
    [InlineData(ComplexityHardFloorTrigger.PublicProtocol, "sol-medium")]
    [InlineData(ComplexityHardFloorTrigger.PersistentStateMigration, "sol-medium")]
    [InlineData(ComplexityHardFloorTrigger.ThreeOrMoreRuntimeSubsystems, "sol-medium")]
    [InlineData(ComplexityHardFloorTrigger.UnclearBug, "terra-medium")]
    public void Route_AllHardFloorsAreEstablishedBeforeQuotaAndNeverCrossed(
        ComplexityHardFloorTrigger trigger,
        string floorRoute)
    {
        var result = ModelRouter.Default.Route(Request(0,
            openAi: AvailabilityWarningState.Warning,
            deterministicVerification: true,
            hardFloor: trigger));

        Assert.Equal(floorRoute, result.RecommendedRoute.RouteId);
        Assert.Equal(floorRoute, result.CorrectnessFloor.RouteId);
        Assert.True(result.CorrectnessFloor.IsHardFloor);
        Assert.NotEqual(ModelRouteSelectionSource.OneTierQuotaDowngrade, result.SelectionSource);
        Assert.Null(result.SelectedRoute);
    }

    [Fact]
    public void Route_OperatorPinIsRetainedSelectedAndFlaggedWhenBelowPolicy()
    {
        var pin = new OperatorModelRoutePin("terra", "medium");
        var result = ModelRouter.Default.Route(Request(70, pin: pin));

        Assert.Equal(ModelRoutingDisposition.Selected, result.Disposition);
        Assert.Equal(ModelRouteSelectionSource.OperatorPin, result.SelectionSource);
        Assert.Equal(pin, result.OperatorPin);
        Assert.True(result.OperatorPinBelowPolicy);
        Assert.Equal("gpt-5.6-terra", result.SelectedRoute?.ModelId);
        Assert.Contains("below policy", result.FallbackOrWaitReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Route_WorkflowIncompatiblePinFailsClosedAndRemainsVisible()
    {
        var pin = new OperatorModelRoutePin("mini", "high");
        var result = ModelRouter.Default.Route(Request(21, pin: pin));

        Assert.Equal(ModelRoutingDisposition.OverrideRequired, result.Disposition);
        Assert.Equal(pin, result.OperatorPin);
        Assert.Null(result.SelectedRoute);
        Assert.Contains("not selectable for CoreTask", result.FallbackOrWaitReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_MiniHighIsLimitedToBoundedStructuredDeterministicDecisions()
    {
        var workflow = new ModelRoutingWorkflow
        {
            Role = RoutingWorkflowRole.BoundedPipelineDecision,
            EvidenceIsCompactAndStructured = true,
            HasDeterministicOutputContract = true,
        };
        var result = ModelRouter.Default.Route(Request(0, workflow: workflow));

        Assert.Equal("mini-high", result.RecommendedRoute.RouteId);
        Assert.Equal("mini-high", result.SelectedRoute?.RouteId);
        Assert.Equal(RoutingWorkflowRole.BoundedPipelineDecision, result.SelectedRoute?.WorkflowRole);
        Assert.Equal(EffortLevel.High, result.SelectedRoute?.EfficiencySuggestion.SuggestedEffort);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Route_BoundedWorkflowMissingRequiredCapabilityFailsClosedToSol(
        bool structured,
        bool deterministicOutput)
    {
        var result = ModelRouter.Default.Route(Request(0, workflow: new()
        {
            Role = RoutingWorkflowRole.BoundedPipelineDecision,
            EvidenceIsCompactAndStructured = structured,
            HasDeterministicOutputContract = deterministicOutput,
        }));

        Assert.Equal("sol-medium", result.RecommendedRoute.RouteId);
        Assert.Equal("sol-medium", result.SelectedRoute?.RouteId);
        Assert.Equal(RoutingWorkflowRole.BoundedPipelineDecision, result.SelectedRoute?.WorkflowRole);
        Assert.Equal("sol-medium", result.CorrectnessFloor.RouteId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Route_AmbiguousOrUnboundedAuthorizingDecisionUsesSolMedium(bool ambiguous, bool unbounded)
    {
        var result = ModelRouter.Default.Route(Request(0, workflow: new()
        {
            Role = RoutingWorkflowRole.BoundedPipelineDecision,
            EvidenceIsCompactAndStructured = true,
            HasDeterministicOutputContract = true,
            EvidenceIsAmbiguous = ambiguous,
            EvidenceIsUnbounded = unbounded,
            AuthorizingTriggers = ["laneAffectingAction"],
        }));

        Assert.Equal("sol-medium", result.RecommendedRoute.RouteId);
        Assert.Equal("sol-medium", result.CorrectnessFloor.RouteId);
        Assert.NotEqual("gpt-5.4-mini", result.SelectedRoute?.ModelId);
    }

    [Fact]
    public void Route_UnboundedNonAuthorizingDecisionStillCannotUseMini()
    {
        var result = ModelRouter.Default.Route(Request(0, workflow: new()
        {
            Role = RoutingWorkflowRole.BoundedPipelineDecision,
            EvidenceIsCompactAndStructured = true,
            HasDeterministicOutputContract = true,
            EvidenceIsUnbounded = true,
        }));

        Assert.Equal("sol-medium", result.RecommendedRoute.RouteId);
        Assert.Equal("sol-medium", result.CorrectnessFloor.RouteId);
    }

    [Theory]
    [InlineData(AvailabilityWarningState.Critical, ModelRoutingDisposition.Wait)]
    [InlineData(AvailabilityWarningState.Unknown, ModelRoutingDisposition.OverrideRequired)]
    public void Route_UnavailableSafeRouteReturnsExplicitTerminalDecision(
        AvailabilityWarningState state,
        ModelRoutingDisposition disposition)
    {
        var result = ModelRouter.Default.Route(Request(20, openAi: state));

        Assert.Equal(disposition, result.Disposition);
        Assert.Null(result.SelectedRoute);
        Assert.NotEmpty(result.FallbackOrWaitReason);
        AssertCompleteAudit(result, 20);
    }

    [Fact]
    public void Route_RestrictedTrustEvidenceFailsClosed()
    {
        var assessment = Trust("gpt-5.6-terra", TrustLevel.Restricted);
        var result = ModelRouter.Default.Route(Request(21, trust: [assessment]));

        Assert.Equal(ModelRoutingDisposition.OverrideRequired, result.Disposition);
        Assert.Null(result.SelectedRoute);
        Assert.Same(assessment, result.RecommendedRoute.TrustEvidence);
    }

    [Fact]
    public void Route_ReissueHumanStopRequiresOverrideWithoutDroppingAuditFields()
    {
        var result = ModelRouter.Default.Route(Request(21,
            previousOutcome: RoutingAttemptOutcome.SemanticFailure,
            failuresAtStrongerTier: 2));

        Assert.Equal(ModelRoutingDisposition.OverrideRequired, result.Disposition);
        Assert.Equal(ModelRouteSelectionSource.OverrideRequired, result.SelectionSource);
        Assert.Null(result.SelectedRoute);
        Assert.Equal(31, result.ScoreWorksheet.EffectivePolicyScore);
        AssertCompleteAudit(result, 21);
    }

    [Fact]
    public void Route_ExplicitOperatorPinStillWinsAfterEscalationStops()
    {
        var pin = new OperatorModelRoutePin("sol", "xhigh");
        var result = ModelRouter.Default.Route(Request(21,
            pin: pin,
            previousOutcome: RoutingAttemptOutcome.SemanticFailure,
            failuresAtStrongerTier: 2));

        Assert.Equal(ModelRoutingDisposition.Selected, result.Disposition);
        Assert.Equal(ModelRouteSelectionSource.OperatorPin, result.SelectionSource);
        Assert.Equal(pin, result.OperatorPin);
        Assert.False(result.OperatorPinBelowPolicy);
    }

    [Fact]
    public void Route_SemanticReissuePromotionCannotBeUndoneByQuotaDowngrade()
    {
        var result = ModelRouter.Default.Route(Request(45,
            openAi: AvailabilityWarningState.Warning,
            deterministicVerification: true,
            previousOutcome: RoutingAttemptOutcome.SemanticFailure));

        Assert.Equal("sol-medium", result.RecommendedRoute.RouteId);
        Assert.Equal("sol-medium", result.CorrectnessFloor.RouteId);
        Assert.Contains("semanticReissuePromotion", result.CorrectnessFloor.AppliedFloorIds);
        Assert.Equal(ModelRoutingDisposition.Wait, result.Disposition);
        Assert.NotEqual(ModelRouteSelectionSource.OneTierQuotaDowngrade, result.SelectionSource);
    }

    private static ModelRoutingSelectionRequest Request(
        int score,
        AvailabilityWarningState openAi = AvailabilityWarningState.Healthy,
        AvailabilityWarningState? claude = null,
        bool deterministicVerification = false,
        RoutingEvidenceReport? benchmark = null,
        string? requiredBenchmarkCapability = null,
        ComplexityHardFloorTrigger? hardFloor = null,
        OperatorModelRoutePin? pin = null,
        ModelRoutingWorkflow? workflow = null,
        IReadOnlyList<ModelTrustAssessment>? trust = null,
        RoutingAttemptOutcome previousOutcome = RoutingAttemptOutcome.None,
        int failuresAtStrongerTier = 0)
    {
        var task = new ComplexityCard { TaskKey = "TE-test", Prompt = "Implement the specified feature.", TaskType = "feature" };
        var rows = new List<ProviderAvailabilitySnapshotRow> { Provider("openai", "codex", openAi) };
        var clis = new List<Cli> { Cli.Codex };
        if (claude is { } claudeState)
        {
            rows.Add(Provider("anthropic", "claude", claudeState));
            clis.Add(Cli.Claude);
        }
        return new()
        {
            Task = task,
            UpfrontEstimate = Estimate(score, hardFloor),
            Capacity = new()
            {
                ProviderAvailability = new(DecisionAt, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), rows),
                DeterministicVerificationAvailable = deterministicVerification,
            },
            AvailableClis = clis,
            BenchmarkQualification = benchmark,
            RequiredBenchmarkCapability = requiredBenchmarkCapability,
            TrustEvidence = trust ?? [],
            Workflow = workflow ?? new(),
            OperatorPin = pin,
            PreviousOutcome = previousOutcome,
            SemanticFailuresAtStrongerTier = failuresAtStrongerTier,
        };
    }

    private static ProviderAvailabilitySnapshotRow Provider(
        string provider,
        string cli,
        AvailabilityWarningState warning)
        => new(
            provider,
            cli,
            ProviderCliAvailability.Available,
            null,
            DecisionAt.AddMinutes(-1),
            SnapshotFreshness.Fresh,
            warning,
            0,
            0,
            new(DecisionAt, SnapshotCostStatus.Priced, []),
            [new("five-hour", null, DecisionAt.AddHours(2), SnapshotFreshness.Fresh, warning,
                new(SnapshotValueOrigin.Observed, warning == AvailabilityWarningState.Critical ? 100 : 10, 100,
                    warning == AvailabilityWarningState.Critical ? 0 : 90,
                    warning == AvailabilityWarningState.Critical ? 100 : 10, DecisionAt.AddMinutes(-1)), null)],
            []);

    private static TaskComplexityEstimate Estimate(int score, ComplexityHardFloorTrigger? floor)
    {
        var remaining = score;
        var risk = Take(ref remaining, 35);
        var scope = Take(ref remaining, 20);
        var context = Take(ref remaining, 20);
        var uncertainty = Take(ref remaining, 10);
        var empirical = Take(ref remaining, 10);
        var quota = Take(ref remaining, 5);
        Assert.Equal(0, remaining);
        var floors = floor is null
            ? Array.Empty<ComplexityHardFloor>()
            : [new ComplexityHardFloor(floor.Value, FloorBand(floor.Value), "Explicit test floor.")];
        return new()
        {
            TaskKey = "TE-test",
            Level = Level(score),
            Score = score,
            ScoreEvidence = $"Test worksheet {score}/100.",
            Confidence = .8,
            ConfidenceEvidence = "Test confidence.",
            PredictedTokens = 1,
            PredictedDuration = TimeSpan.FromSeconds(1),
            PredictedReissues = 0,
            TokenForecast = new(1, 1, 1, "test"),
            DurationForecast = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), "test"),
            ReissueForecast = new(0, 0, 0, "test"),
            CorrectnessRisk = Criterion("correctness_risk", risk, 35),
            ExpectedScope = Criterion("expected_scope", scope, 20),
            ContextDemand = Criterion("context_demand", context, 20),
            TaskUncertainty = Criterion("task_uncertainty", uncertainty, 10),
            EmpiricalConfidence = Criterion("empirical_confidence", empirical, 10),
            QuotaAndCostHeadroom = Criterion("quota_and_cost_headroom", quota, 5),
            HardFloors = floors,
            HistoricalEvidence = new(0, 0, 0, 0, 0, 0, 0, "none"),
            Dimensions = [],
            Neighbours = [],
        };
    }

    private static ComplexityRoutingCriterion Criterion(string name, int score, int maximum)
        => new(name, score, maximum, $"{name} test evidence.");

    private static int Take(ref int remaining, int maximum)
    {
        var value = Math.Min(remaining, maximum);
        remaining -= value;
        return value;
    }

    private static TaskComplexityLevel Level(int score) => score switch
    {
        <= 20 => TaskComplexityLevel.Trivial,
        <= 50 => TaskComplexityLevel.Standard,
        <= 69 => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };

    private static TaskComplexityLevel FloorBand(ComplexityHardFloorTrigger trigger) => trigger switch
    {
        ComplexityHardFloorTrigger.UnclearBug => TaskComplexityLevel.Standard,
        ComplexityHardFloorTrigger.PublicProtocol or ComplexityHardFloorTrigger.PersistentStateMigration
            or ComplexityHardFloorTrigger.ThreeOrMoreRuntimeSubsystems => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };

    private static RoutingEvidenceReport EmptyEvidence() => new()
    {
        EvidenceVersion = "test-evidence-v1",
        ConfidenceGates = new(),
        ControlledCohorts = [],
        ObservationalCohorts = [],
    };

    private static RoutingEvidenceReport EvidenceFor(string model, string thinking, RoutingQualificationLevel level)
    {
        var cohort = new RoutingEvidenceCohort
        {
            CanonicalModel = model,
            ThinkingLevel = thinking,
            TaskClass = "feature",
            Capability = "repository-editing",
            SampleSize = 5,
            AttemptLevelRouteCount = 5,
            CardLevelRouteCount = 0,
            UnknownRouteCount = 0,
            OutcomeAvailableCount = 5,
            SuccessCount = 5,
            SuccessRate = 1,
            OutcomeCoverage = 1,
            GradeAvailableCount = 5,
            FavorableGradeCount = 5,
            GradeCoverage = 1,
            FavorableGradeRate = 1,
            SemanticReissueAvailableCount = 5,
            SemanticReissueCount = 0,
            SemanticReissueCoverage = 1,
            SemanticReissueRate = 0,
            DurationAvailableCount = 5,
            DurationCoverage = 1,
            TotalDurationMs = 5,
            AverageDurationMs = 1,
            TokenAvailableCount = 5,
            TokenCoverage = 1,
            TotalTokens = 5,
            AverageTokens = 1,
            CostAvailableCount = 5,
            CostCoverage = 1,
            TotalCostUsd = 1,
            ObservedFrom = new(2026, 7, 1),
            ObservedThrough = new(2026, 7, 31),
            Provenance = [new() { ArtifactReference = "benchmark.json" }],
            Qualification = new()
            {
                Level = level,
                ClaimsValidation = level == RoutingQualificationLevel.Validated,
                GateFailures = level == RoutingQualificationLevel.Validated ? [] : ["sample size below gate"],
            },
        };
        return new()
        {
            EvidenceVersion = "test-evidence-v1",
            ConfidenceGates = new(),
            ControlledCohorts = [],
            ObservationalCohorts = [cohort],
        };
    }

    private static ModelTrustAssessment Trust(string model, TrustLevel level) => new()
    {
        ModelId = model,
        Level = level,
        CapabilityClaimCount = 0,
        IndependentProofCount = 0,
        ObservedRunCount = 0,
        ViolationsObserved = 0,
        ViolationRate = null,
        Sources = [],
        OpenIncidents = [],
        Rationale = "Test trust assessment.",
    };

    private static void AssertCompleteAudit(ModelRoutingResult result, int expectedScore)
    {
        Assert.NotNull(result.RecommendedRoute);
        Assert.Equal(expectedScore, result.ScoreWorksheet.Total);
        Assert.Equal(6, result.ScoreWorksheet.Criteria.Count);
        Assert.True(result.ScoreWorksheet.EffectivePolicyScore >= 0);
        Assert.NotNull(result.CorrectnessFloor);
        Assert.Equal("2026-07-24", result.PolicyVersion);
        Assert.Equal(1, result.ModelKnowledgeSchemaVersion);
        Assert.Equal("2026-07-24", result.ModelKnowledgeEvidenceVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.BenchmarkEvidenceVersion));
        Assert.False(string.IsNullOrWhiteSpace(result.FallbackOrWaitReason));
        Assert.False(string.IsNullOrWhiteSpace(result.PolicyReason));
        Assert.NotNull(result.Uncertainty);
    }
}
