using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ProviderQuotaDashboardTests
{
    private static readonly DateTime DecisionAt = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildSnapshot_PreservesMultipleObservedWindowsAndSeparatesInferredProjection()
    {
        var builder = new ProviderQuotaDashboardBuilder();
        ProviderQuotaDashboardEvent? observedEvent = null;
        builder.EventOccurred += value => observedEvent = value;
        var snapshot = builder.BuildSnapshot(
            [MeasuredRun("anthropic", "claude", "claude-sonnet-5", DecisionAt.AddMinutes(-30), 100)],
            SnapshotOptions(
                new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-2), ["claude-sonnet-5"]),
                [
                    new("anthropic", "claude", "five-hour", 800, 1_000, DecisionAt.AddMinutes(-2), DecisionAt.AddHours(3), DecisionAt.AddHours(-2)),
                    new("anthropic", "claude", "weekly", 2_000, 10_000, DecisionAt.AddMinutes(-2), DecisionAt.AddDays(5), DecisionAt.AddDays(-2)),
                ]));

        var row = Assert.Single(snapshot.Providers);
        Assert.Equal(DecisionAt, snapshot.DecisionAtUtc);
        Assert.Equal("claude", row.CliType);
        Assert.Equal(AvailabilityWarningState.Warning, row.WarningState);
        Assert.Equal(SnapshotCostStatus.Priced, row.Cost.Status);
        Assert.Equal(2, row.QuotaWindows.Count);

        var shortWindow = row.QuotaWindows.Single(window => window.WindowId == "five-hour");
        Assert.Equal(SnapshotValueOrigin.Observed, shortWindow.Usage?.Origin);
        Assert.Equal(800, shortWindow.Usage?.UsedTokens);
        Assert.Equal(200, shortWindow.Usage?.HeadroomTokens);
        Assert.Equal(SnapshotValueOrigin.Inferred, shortWindow.Projection?.Origin);
        Assert.Equal(DecisionAt.AddHours(2), shortWindow.Projection?.ProjectedExhaustionAtUtc);
        Assert.True(shortWindow.Projection?.ExhaustsBeforeReset);
        Assert.Equal(2_000, row.QuotaWindows.Single(window => window.WindowId == "weekly").Usage?.UsedTokens);
        Assert.Equal("provider_availability.snapshot.built", observedEvent?.Name);
        Assert.Equal(2, observedEvent?.Context["quotaWindowCount"]);
    }

    [Fact]
    public void BuildSnapshot_ExhaustedWindowIsCriticalWithZeroObservedHeadroom()
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1), ["claude-sonnet-5"]),
            new("anthropic", "claude", "five-hour", 1_100, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2)));

        var window = Assert.Single(row.QuotaWindows);
        Assert.Equal(AvailabilityWarningState.Critical, row.WarningState);
        Assert.Equal(AvailabilityWarningState.Critical, window.WarningState);
        Assert.Equal(0, window.Usage?.HeadroomTokens);
        Assert.Null(window.Projection);
    }

    [Fact]
    public void BuildSnapshot_StaleLowUsageIsUnknownRatherThanHealthy()
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddHours(-2), ["claude-sonnet-5"]),
            new("anthropic", "claude", "five-hour", 10, 1_000, DecisionAt.AddHours(-2), DecisionAt.AddHours(2)));

        Assert.Equal(SnapshotFreshness.Stale, row.Freshness);
        Assert.Equal(AvailabilityWarningState.Unknown, row.WarningState);
        Assert.Equal(AvailabilityWarningState.Unknown, Assert.Single(row.QuotaWindows).WarningState);
        Assert.Null(Assert.Single(row.QuotaWindows).Projection);
    }

    [Fact]
    public void BuildSnapshot_UnavailableCliIsCriticalEvenWithFreshHeadroom()
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Unavailable, DecisionAt.AddMinutes(-1), ["claude-sonnet-5"], "CLI probe failed"),
            new("anthropic", "claude", "five-hour", 10, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2)));

        Assert.Equal(ProviderCliAvailability.Unavailable, row.Availability);
        Assert.Equal(AvailabilityWarningState.Critical, row.WarningState);
    }

    [Theory]
    [InlineData("never-seen-model", SnapshotCostStatus.Unknown)]
    [InlineData("gpt-5.6-sol", SnapshotCostStatus.Unpriced)]
    public void BuildSnapshot_UnknownOrUnpricedCostIsNeverHealthy(string model, SnapshotCostStatus expected)
    {
        var row = BuildSingle(
            new("openai", "codex", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1), [model]),
            new("openai", "codex", "five-hour", 10, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2)));

        Assert.Equal(expected, row.Cost.Status);
        Assert.Equal(AvailabilityWarningState.Unknown, row.WarningState);
        var html = ProviderQuotaDashboardHtmlRenderer.RenderSnapshot(new(DecisionAt, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), [row]));
        Assert.DoesNotContain("$0", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expected == SnapshotCostStatus.Unknown ? "Unknown model cost" : "Unpriced at decision time", html);
    }

    [Fact]
    public void BuildSnapshot_UnconfirmedCostAndUnknownAvailabilityAreNeverHealthy()
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Unknown, DecisionAt.AddMinutes(-1), ["claude-sonnet-4-5"]),
            new("anthropic", "claude", "five-hour", 10, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2)));

        Assert.Equal(SnapshotCostStatus.Unconfirmed, row.Cost.Status);
        Assert.Equal(AvailabilityWarningState.Unknown, row.WarningState);
    }

    [Fact]
    public void BuildSnapshot_UsesExecutionTimeRatherThanLaterImportObservationForRate()
    {
        var oldRun = MeasuredRun("anthropic", "claude", "claude-sonnet-5", DecisionAt.AddHours(-3), 100) with
        {
            ObservedAtUtc = DecisionAt.AddMinutes(-1),
        };
        var snapshot = new ProviderQuotaDashboardBuilder().BuildSnapshot(
            [oldRun],
            SnapshotOptions(
                new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1), ["claude-sonnet-5"]),
                [new("anthropic", "claude", "five-hour", 100, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2))]));

        var row = Assert.Single(snapshot.Providers);
        Assert.Equal(0, row.TrailingTokens);
        Assert.Equal(0, row.TokensPerHour);
        Assert.Null(Assert.Single(row.QuotaWindows).Projection);
    }

    [Theory]
    [InlineData(null, 100L, SnapshotFreshness.Missing)]
    [InlineData(-1L, 100L, SnapshotFreshness.Suspicious)]
    public void BuildSnapshot_MissingOrSuspiciousQuotaIsNeverHealthy(long? used, long? limit, SnapshotFreshness expected)
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1), ["claude-sonnet-5"]),
            new("anthropic", "claude", "five-hour", used, limit, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(2)));

        var window = Assert.Single(row.QuotaWindows);
        Assert.Equal(expected, window.Freshness);
        Assert.Equal(AvailabilityWarningState.Unknown, row.WarningState);
    }

    [Fact]
    public void RenderSnapshot_ShowsDecisionEvidenceOriginsAvailabilityAndEveryWindow()
    {
        var row = BuildSingle(
            new("anthropic", "claude", ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1), ["claude-sonnet-5"]),
            new("anthropic", "claude", "five-hour", 800, 1_000, DecisionAt.AddMinutes(-1), DecisionAt.AddHours(3)));
        var html = ProviderQuotaDashboardHtmlRenderer.RenderSnapshot(new(DecisionAt, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), [row]));

        Assert.Contains("Decision time", html);
        Assert.Contains("CLI claude", html);
        Assert.Contains("CLI availability", html);
        Assert.Contains("Observed provider quota telemetry", html);
        Assert.Contains("not provider-observed", html);
        Assert.Contains("Observed headroom", html);
        Assert.Contains("Reset", html);
        Assert.Contains("Cost at decision time", html);
        Assert.Contains("five-hour", html);
        Assert.Contains("Availability evidence only; no model selection is performed", html);
    }

    [Fact]
    public void Build_ReproducesHistoricalSpikeAndProjectsQuotaMark()
    {
        var asOf = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var builder = new ProviderQuotaDashboardBuilder();
        ProviderQuotaDashboardEvent? observed = null;
        builder.EventOccurred += e => observed = e;
        var rows = builder.Build(new[]
        {
            Run("openai", "gpt-5", asOf.AddMinutes(-20), 600),
            Run("openai", "gpt-5-mini", asOf.AddMinutes(-10), 300),
            Run("anthropic", "claude-sonnet", asOf.AddMinutes(-10), 100),
            Run("openai", "gpt-5", asOf.AddHours(-2), 800), // outside the one-hour rate but inside quota window
        }, new(asOf, TimeSpan.FromHours(1), TimeSpan.FromHours(5), new[]
        {
            new ProviderQuotaMark("openai", 2_000), new ProviderQuotaMark("anthropic", 1_000), new ProviderQuotaMark("google", 1_000)
        }, new(50, 80)));

        var openAi = rows.Single(r => r.Provider == "openai");
        Assert.Equal(900, openAi.TrailingTokens);
        Assert.Equal(900m, openAi.TokensPerHour);
        Assert.Equal(1_700, openAi.QuotaWindowTokens);
        Assert.Equal(85m, openAi.QuotaMarkPercent);
        Assert.Equal(QuotaVisualState.Critical, openAi.State);
        Assert.Equal(asOf.AddMinutes(20), openAi.ProjectedMarkAtUtc);
        Assert.Equal(new[] { "Balanced", "Unknown" }, openAi.ModelShares.Select(s => s.Tier));
        Assert.Equal(1400, openAi.ModelShares[0].Tokens);
        Assert.Equal(82.352941176470588235294117647m, openAi.ModelShares[0].Percent);
        Assert.Equal("provider_quota.dashboard.built", observed?.Name);
        Assert.Equal(3, observed?.Context["providerCount"]);
    }

    [Fact]
    public void Build_DoesNotInventProjectionWithoutRecentConsumption()
    {
        var asOf = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build(new[] { Run("google", "gemini", asOf.AddHours(-2), 20) },
            new(asOf, TimeSpan.FromHours(1), TimeSpan.FromHours(5), new[] { new ProviderQuotaMark("google", 100) })));

        Assert.Equal(20, row.QuotaWindowTokens);
        Assert.Equal(0, row.TokensPerHour);
        Assert.Null(row.ProjectedMarkAtUtc);
        Assert.Equal(QuotaVisualState.Ok, row.State);
    }

    [Fact]
    public void Render_ContainsVisibleStateProjectionAndTierShare()
    {
        var row = new ProviderQuotaDashboardRow("openai", 900, 900, 1700, 2000, 85, 300,
            new DateTime(2026, 7, 23, 12, 20, 0, DateTimeKind.Utc), QuotaVisualState.Critical,
            [new("Balanced", 1400, 82.35m), new("Unknown", 300, 17.65m)]);

        var html = ProviderQuotaDashboardHtmlRenderer.Render([row]);

        Assert.Contains("tokens/hour", html);
        Assert.Contains("2026-07-23 12:20 UTC", html);
        Assert.Contains("quota-card quota-critical", html);
        Assert.Contains("Active-window tier share", html);
        Assert.Contains("Balanced</span><span class=\"tier-bar\"", html);
        Assert.Contains("aria-valuenow=\"85\"", html);
        Assert.Contains("quota-critical", html);
    }

    [Fact]
    public void Render_MakesAnIdleProviderVisibleWithoutInventingATierShare()
    {
        var row = new ProviderQuotaDashboardRow("google", 0, 0, 0, 1_000, 0, 1_000,
            null, QuotaVisualState.Ok, []);

        var html = ProviderQuotaDashboardHtmlRenderer.Render([row]);

        Assert.Contains("quota-card quota-ok", html);
        Assert.Contains("No recent rate", html);
        Assert.Contains("No active-window consumption", html);
    }

    [Fact]
    public void Build_GroupsDifferentModelsIntoTheirCapabilityTier()
    {
        var asOf = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build(
            [Run("openai", "gpt-5", asOf.AddMinutes(-10), 400), Run("openai", "gpt-5.5", asOf.AddMinutes(-5), 600)],
            new(asOf, TimeSpan.FromHours(1), TimeSpan.FromHours(5), [new ProviderQuotaMark("openai", 2_000)])));

        var share = Assert.Single(row.ModelShares);
        Assert.Equal("Balanced", share.Tier);
        Assert.Equal(1_000, share.Tokens);
        Assert.Equal(100m, share.Percent);
    }

    private static AgentStudioRunRecord Run(string provider, string model, DateTime observedAt, long input) => new()
    {
        TaskKey = Guid.NewGuid().ToString("N"), Run = 1, Provider = provider, Model = model, Usage = new(input, 0, 0, 0),
        ExecutedAtUtc = observedAt, ObservedAtUtc = observedAt, CostStatus = PriceStatus.UnknownModel, Outcome = OutcomeQualitySignal.Unknown
    };

    private static AgentStudioRunRecord MeasuredRun(string provider, string cli, string model, DateTime observedAt, long input) => new()
    {
        TaskKey = Guid.NewGuid().ToString("N"), Run = 1, Provider = provider, CliType = cli, Model = model,
        Usage = new(input, 0, 0, 0), TokenUsageAvailable = true, ExecutedAtUtc = observedAt,
        ObservedAtUtc = observedAt, CostStatus = PriceStatus.Resolved, Outcome = OutcomeQualitySignal.Unknown,
    };

    private static ProviderAvailabilitySnapshotOptions SnapshotOptions(
        ProviderCliObservation provider,
        IReadOnlyCollection<ProviderQuotaWindowObservation> windows) => new(
            DecisionAt, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), [provider], windows, new(75, 90));

    private static ProviderAvailabilitySnapshotRow BuildSingle(
        ProviderCliObservation provider,
        ProviderQuotaWindowObservation window) => Assert.Single(new ProviderQuotaDashboardBuilder().BuildSnapshot(
            [MeasuredRun(provider.Provider, provider.CliType, provider.ModelIds.Single(), DecisionAt.AddMinutes(-30), 100)],
            SnapshotOptions(provider, [window])).Providers);
}
