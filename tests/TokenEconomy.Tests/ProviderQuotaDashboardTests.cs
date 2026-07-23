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
        Assert.Equal(new[] { "gpt-5", "gpt-5-mini" }, openAi.ModelShares.Select(s => s.Model));
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

    private static AgentStudioRunRecord Run(string provider, string model, DateTime observedAt, long input) => new()
    {
        TaskKey = Guid.NewGuid().ToString("N"), Run = 1, Provider = provider, Model = model, Usage = new(input, 0, 0, 0),
        ExecutedAtUtc = observedAt, ObservedAtUtc = observedAt, CostStatus = PriceStatus.UnknownModel, Outcome = OutcomeQualitySignal.Unknown
    };
}
