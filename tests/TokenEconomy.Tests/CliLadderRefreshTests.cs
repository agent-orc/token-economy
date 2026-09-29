using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

/// <summary>CLI thinking ladders verified on 2026-09-29 against Claude Code and Codex documentation and runner probes (TE-59).</summary>
public sealed class CliLadderRefreshTests
{
    [Theory]
    [InlineData("gpt-6-astra", "low,medium,high,xhigh,max,ultra")]
    [InlineData("gpt-6-sol", "low,medium,high,xhigh,max,ultra")]
    [InlineData("gpt-6-luna", "low,medium,high,xhigh,max")]
    [InlineData("gpt-5.6-sol", "low,medium,high,xhigh,max,ultra")]
    [InlineData("gpt-5.6-terra", "low,medium,high,xhigh,max,ultra")]
    [InlineData("gpt-5.6-luna", "low,medium,high,xhigh,max")]
    [InlineData("gpt-5.5", "low,medium,high,xhigh")]
    [InlineData("claude-fable-5-1", "low,medium,high,xhigh,max")]
    [InlineData("claude-opus-5-5", "low,medium,high,xhigh,max")]
    [InlineData("claude-sonnet-5-5", "low,medium,high,xhigh,max")]
    [InlineData("claude-sonnet-5", "low,medium,high,xhigh,max")]
    [InlineData("claude-opus-5", "low,medium,high,xhigh,max")]
    [InlineData("claude-opus-4-8", "low,medium,high,xhigh,max")]
    [InlineData("claude-opus-4-7", "low,medium,high,xhigh,max")]
    [InlineData("claude-sonnet-4-6", "low,medium,high,max")]
    public void LadderMatchesTheCliEvidence(string model, string levels)
        => Assert.Equal(levels.Split(','), ModelRoutingKnowledgeBase.Default.FindModel(model)!.SupportedThinkingLevels);

    [Fact]
    public void RetiredCodexChatGptModelsCarryAnAvailabilityWarningWithoutChangingRoutes()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;

        Assert.Contains("HTTP 400", knowledge.FindModel("gpt-5.4-mini")!.Note);
        Assert.Equal("gpt-5.4-mini", knowledge.FindRoute("mini-high")!.ModelId);
        Assert.Contains("2026-10-14", knowledge.FindModel("gpt-5.5")!.Note);
        Assert.Contains("do not support effort", knowledge.FindModel("claude-haiku-4-5")!.Note);
    }
}
