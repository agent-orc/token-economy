using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ProviderQuotaDashboardTests
{
    private static readonly DateTime DecisionAt = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_ReportsExhaustedObservedWindowAndSeparateProjection()
    {
        var builder = new ProviderQuotaDashboardBuilder();
        ProviderQuotaDashboardEvent? observedEvent = null;
        builder.EventOccurred += value => observedEvent = value;
        var mark = Window("anthropic", "claude", "five-hour", 2_000, 2_000, DecisionAt.AddMinutes(-2), "claude-sonnet-5") with
        {
            WindowLabel = "Five-hour window", ResetsAtUtc = DecisionAt.AddHours(3),
        };

        var row = Assert.Single(builder.Build(
            [Run("anthropic", "claude", "claude-sonnet-5", DecisionAt.AddMinutes(-10), 200)],
            Options([mark], Available("anthropic", "claude"))));

        Assert.Equal(QuotaVisualState.Critical, row.State);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.Exhausted));
        Assert.Equal(ProviderCliAvailability.Available, row.Availability);
        Assert.Equal(QuotaUsageSource.ObservedProviderMeter, row.QuotaObservation!.Source);
        Assert.Equal(2_000, row.QuotaObservation.UsedTokens);
        Assert.Equal(0, row.QuotaObservation.HeadroomTokens);
        Assert.Equal(DecisionAt.AddHours(3), row.QuotaObservation.ResetsAtUtc);
        Assert.Equal(DecisionAt, row.Projection!.ProjectedExhaustionAtUtc);
        Assert.Equal(ProviderCostStatus.Priced, row.CostSnapshot!.Status);
        Assert.Equal(DecisionAt, row.CostSnapshot.PricedAtUtc);
        Assert.Equal("provider_quota.availability_snapshot.built", observedEvent?.Name);
        Assert.Equal(1, observedEvent?.Context["quotaWindowCount"]);
    }

    [Fact]
    public void Build_ReportsNearCapWithoutCallingItHealthy()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("anthropic", "claude", "five-hour", 1_000, 800, DecisionAt.AddMinutes(-1), "claude-sonnet-5")],
            Available("anthropic", "claude"))));

        Assert.Equal(QuotaVisualState.Warning, row.State);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.NearCap));
        Assert.Equal(200, row.QuotaObservation!.HeadroomTokens);
        Assert.Null(row.Projection!.ProjectedExhaustionAtUtc);
    }

    [Fact]
    public void Build_StaleQuotaObservationIsNeverHealthy()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("anthropic", "claude", "five-hour", 1_000, 100, DecisionAt.AddMinutes(-16), "claude-sonnet-5")],
            Available("anthropic", "claude"))));

        Assert.Equal(AvailabilityFreshness.Stale, row.QuotaObservation!.Freshness);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.Stale));
        Assert.NotEqual(QuotaVisualState.Ok, row.State);
    }

    [Fact]
    public void Build_UnavailableCliIsCriticalEvenWithFreshHeadroom()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("anthropic", "claude", "five-hour", 1_000, 100, DecisionAt.AddMinutes(-1), "claude-sonnet-5")],
            new ProviderCliAvailabilityObservation("anthropic", "claude", ProviderCliAvailability.Unavailable, DecisionAt.AddMinutes(-1), Detail: "authentication failed"))));

        Assert.Equal(QuotaVisualState.Critical, row.State);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.Unavailable));
        Assert.Contains("authentication failed", row.WarningReasons.Single(reason => reason.StartsWith("provider CLI unavailable", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_UnknownCostIsNeverReportedAsHealthyOrFree()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("unknown-vendor", "mystery-cli", "daily", 1_000, 100, DecisionAt.AddMinutes(-1), "mystery-model")],
            Available("unknown-vendor", "mystery-cli"))));

        Assert.Equal(ProviderCostStatus.Unknown, row.CostSnapshot!.Status);
        Assert.Equal(PriceStatus.UnknownModel, Assert.Single(row.CostSnapshot.Models).Status);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.UnknownCost));
        Assert.NotEqual(QuotaVisualState.Ok, row.State);
        Assert.DoesNotContain("free", row.WarningReasons, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_UnpricedKnownModelIsNeverReportedAsHealthy()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("openai", "codex", "five-hour", 1_000, 100, DecisionAt.AddMinutes(-1), "gpt-5.6-sol")],
            Available("openai", "codex"))));

        Assert.Equal(ProviderCostStatus.Unpriced, row.CostSnapshot!.Status);
        Assert.Equal(PriceStatus.NoPriceForDate, Assert.Single(row.CostSnapshot.Models).Status);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.UnpricedCost));
        Assert.NotEqual(QuotaVisualState.Ok, row.State);
    }

    [Fact]
    public void Build_MissingAndSuspiciousDataNeverAppearHealthy()
    {
        var missing = Window("anthropic", "claude", "five-hour", 1_000, null, null, "claude-sonnet-5");
        var suspicious = Window("anthropic", "claude", "weekly", 10_000, 100, DecisionAt.AddMinutes(-1), "claude-sonnet-5") with { Suspicious = true };
        var rows = new ProviderQuotaDashboardBuilder().Build([], Options([missing, suspicious], Available("anthropic", "claude")));

        var missingRow = rows.Single(row => row.WindowId == "five-hour");
        Assert.Equal(AvailabilityFreshness.Missing, missingRow.QuotaObservation!.Freshness);
        Assert.True(missingRow.WarningState.HasFlag(ProviderQuotaWarning.Missing));
        Assert.Equal(QuotaVisualState.Warning, missingRow.State);
        var suspiciousRow = rows.Single(row => row.WindowId == "weekly");
        Assert.Equal(AvailabilityFreshness.Suspicious, suspiciousRow.QuotaObservation!.Freshness);
        Assert.True(suspiciousRow.WarningState.HasFlag(ProviderQuotaWarning.Suspicious));
        Assert.Equal(QuotaVisualState.Critical, suspiciousRow.State);
    }

    [Fact]
    public void Build_PreservesMultipleQuotaWindowsWithoutCollapsingTheirLimits()
    {
        var rows = new ProviderQuotaDashboardBuilder().Build([], Options([
            Window("anthropic", "claude", "five-hour", 1_000, 250, DecisionAt.AddMinutes(-1), "claude-sonnet-5") with { WindowLabel = "Five-hour" },
            Window("anthropic", "claude", "weekly", 20_000, 10_000, DecisionAt.AddMinutes(-1), "claude-sonnet-5") with { WindowLabel = "Weekly", WindowDuration = TimeSpan.FromDays(7) },
        ], Available("anthropic", "claude")));

        Assert.Equal(2, rows.Count);
        Assert.Equal(1_000, rows.Single(row => row.WindowId == "five-hour").QuotaMarkTokens);
        Assert.Equal(20_000, rows.Single(row => row.WindowId == "weekly").QuotaMarkTokens);
        Assert.Equal(TimeSpan.FromDays(7), rows.Single(row => row.WindowId == "weekly").WindowDuration);
    }

    [Fact]
    public void Build_LabelsImportedRunQuotaAsInferenceAndProjectsFromRecentRate()
    {
        var mark = new ProviderQuotaMark("anthropic", 2_000)
        {
            Cli = "claude", WindowId = "five-hour", WindowLabel = "Five-hour", ModelIds = ["claude-sonnet-5"],
        };
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([
            Run("anthropic", "claude", "claude-sonnet-5", DecisionAt.AddMinutes(-20), 600),
            Run("anthropic", "claude", "claude-sonnet-5", DecisionAt.AddHours(-2), 800),
        ], Options([mark], Available("anthropic", "claude"))));

        Assert.Equal(QuotaUsageSource.InferredFromImportedRuns, row.QuotaObservation!.Source);
        Assert.Equal(1_400, row.QuotaObservation.UsedTokens);
        Assert.Equal(600m, row.Projection!.TokensPerHour);
        Assert.Equal(DecisionAt.AddHours(1), row.Projection.ProjectedExhaustionAtUtc);
        Assert.True(row.WarningState.HasFlag(ProviderQuotaWarning.InferredQuota));
        Assert.NotEqual(QuotaVisualState.Ok, row.State);
    }

    [Fact]
    public void Build_FreshObservedPricedAvailableProviderCanBeHealthy()
    {
        var row = Assert.Single(new ProviderQuotaDashboardBuilder().Build([], Options(
            [Window("anthropic", "claude", "five-hour", 1_000, 100, DecisionAt.AddMinutes(-1), "claude-sonnet-5")],
            Available("anthropic", "claude"))));

        Assert.Equal(ProviderQuotaWarning.None, row.WarningState);
        Assert.Equal(QuotaVisualState.Ok, row.State);
    }

    [Fact]
    public void Render_ShowsDecisionInputsAndNeverFormatsUnknownCostAsFree()
    {
        var rows = new ProviderQuotaDashboardBuilder().Build([], Options([
            Window("anthropic", "claude", "five-hour", 1_000, 800, DecisionAt.AddMinutes(-1), "claude-sonnet-5") with { ResetsAtUtc = DecisionAt.AddHours(3) },
            Window("unknown-vendor", "mystery-cli", "daily", 2_000, 50, DecisionAt.AddMinutes(-1), "mystery-model"),
        ], Available("anthropic", "claude"), Available("unknown-vendor", "mystery-cli")));

        var html = ProviderQuotaDashboardHtmlRenderer.Render(rows);

        Assert.Contains("Provider availability snapshot", html);
        Assert.Contains("Decision time:", html);
        Assert.Contains("2026-07-23 12:00 UTC", html);
        Assert.Contains("claude", html);
        Assert.Contains("five-hour", html);
        Assert.Contains("Quota usage", html);
        Assert.Contains("Headroom", html);
        Assert.Contains("Observed provider meter", html);
        Assert.Contains("Reset", html);
        Assert.Contains("Inferred projection", html);
        Assert.Contains("Cost status at decision time", html);
        Assert.Contains("unknown model cost — not free", html);
        Assert.Contains("Warning state", html);
        Assert.Contains("quota-card quota-warning", html);
        Assert.Contains("aria-valuenow=\"80\"", html);
        Assert.Contains("does not select a model", html);
    }

    private static ProviderQuotaDashboardOptions Options(
        IReadOnlyCollection<ProviderQuotaMark> marks,
        params ProviderCliAvailabilityObservation[] availability) => new(
            DecisionAt, TimeSpan.FromHours(1), TimeSpan.FromHours(5), marks, new(75, 90))
        {
            AvailabilityObservations = availability,
            FreshnessLimit = TimeSpan.FromMinutes(15),
        };

    private static ProviderQuotaMark Window(
        string provider, string cli, string windowId, long limit, long? used, DateTime? observedAt, string model) => new(provider, limit)
        {
            Cli = cli, WindowId = windowId, WindowLabel = windowId, ObservedUsedTokens = used,
            ObservedAtUtc = observedAt, ModelIds = [model],
        };

    private static ProviderCliAvailabilityObservation Available(string provider, string cli)
        => new(provider, cli, ProviderCliAvailability.Available, DecisionAt.AddMinutes(-1));

    private static AgentStudioRunRecord Run(string provider, string cli, string model, DateTime observedAt, long input) => new()
    {
        TaskKey = Guid.NewGuid().ToString("N"), Run = 1, Provider = provider, CliType = cli, Model = model,
        Usage = new(input, 0, 0, 0), TokenUsageAvailable = true, ExecutedAtUtc = observedAt, ObservedAtUtc = observedAt,
        CostStatus = PriceStatus.Resolved, Outcome = OutcomeQualitySignal.Unknown,
    };
}
