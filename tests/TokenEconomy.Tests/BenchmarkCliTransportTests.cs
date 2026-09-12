using Xunit;
namespace TokenEconomy.Tests;

public class BenchmarkCliTransportTests
{
    [Fact]
    public void ExtractsActualModelsFromProviderResultWithoutAuthenticationFields()
    {
        const string payload = """
            {
              "modelUsage": {
                "claude-fable-5-1": {"inputTokens": 10},
                "claude-haiku-4-5": {"inputTokens": 2}
              },
              "apiKey": "must-not-be-collected"
            }
            """;
        Assert.Equal(["claude-fable-5-1", "claude-haiku-4-5"],
            BenchmarkCliTransport.ReportedModels(payload));
    }

    [Fact]
    public void ReadsCodexJsonlAndKeepsAbsentActualIdentityExplicit()
    {
        Assert.Equal(["gpt-6-astra"], BenchmarkCliTransport.ReportedModels(
            "{\"type\":\"session_meta\",\"model\":\"gpt-6-astra\"}\n{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":5}}"));
        Assert.Empty(BenchmarkCliTransport.ReportedModels("{\"type\":\"thread.started\",\"thread_id\":\"example\"}"));
        Assert.Empty(BenchmarkCliTransport.ReportedModels("bad JSON"));
    }

    [Fact]
    public void ProviderFallbackCannotMasqueradeAsRequestedModel()
    {
        Assert.False(BenchmarkCliTransport.MatchesRequested("claude-fable-5-1", ["claude-sonnet-5"]));
        Assert.True(BenchmarkCliTransport.MatchesRequested("claude-fable-5-1", ["claude-fable-5-1-20260901"]));
        Assert.True(BenchmarkCliTransport.MatchesRequested("gpt-5.6-sol", ["gpt-5-6-sol"]));
    }
}
