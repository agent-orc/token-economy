using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ProviderQuotaDashboardTests
{
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
}
