using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class AgentStudioTaskStorageImporterTests
{
    [Fact]
    public void ImportDirectory_MapsMetricsAndIsIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"token-economy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "card-1"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "card-1", "task.json"), """
            { "taskKey":"TE-5", "run":2, "project":"Token-Economy", "model":"claude-sonnet-5",
              "thinkingLevel":"high", "cliType":"claude", "taskType":"feature", "finalLane":"Done",
              "completedAt":"2026-07-10T12:00:00Z", "tokenSummary": { "inputTokens":100000, "outputTokens":20000, "cacheReadTokens":5000 } }
            """);
            var store = new InMemoryAgentStudioRunStore();
            var importer = new AgentStudioTaskStorageImporter();
            var first = importer.ImportDirectory(directory, store);
            var second = importer.ImportDirectory(directory, store);
            var record = Assert.Single(store.Records);
            Assert.Equal(1, first.RecordsUpserted); Assert.Equal(1, second.RecordsUpserted);
            Assert.Equal("TE-5", record.TaskKey); Assert.Equal(2, record.Run);
            Assert.Equal("anthropic", record.Provider); Assert.Equal(100000, record.Usage.Input);
            Assert.Equal(OutcomeQualitySignal.Successful, record.Outcome); Assert.NotNull(record.CostEstimate);
            Assert.Equal(ModelPrice.EstimatedListPricesCaveat, record.CostCaveat);
            Assert.True(record.IsEstimatedListPrice);
            var view = Assert.Single(ModelRunViews.ByModelOverTime(store.Records));
            Assert.Equal(1, view.Runs); Assert.Equal(1, view.SuccessfulRuns); Assert.Equal("Token-Economy", view.Project);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Parse_UsesLastUsageWhenNoSummaryAndRetainsUnknownPrice()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-7", "model":"unpriced-model", "lane":"Blocked", "updatedAt":"2026-07-10T12:00:00Z",
              "lastUsage": { "promptTokens":12, "completionTokens":3 } }
            """);
        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        Assert.Equal(12, record.Usage.Input); Assert.Equal(3, record.Usage.Output);
        Assert.Equal(PriceStatus.UnknownModel, record.CostStatus); Assert.Null(record.CostEstimate);
        Assert.Null(record.CostCaveat); Assert.False(record.IsEstimatedListPrice);
        Assert.Equal(OutcomeQualitySignal.NeedsReview, record.Outcome);
    }

    [Fact]
    public void ImportDirectory_EmitsStructuredFailureEventForUnreadableTask()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"token-economy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "card-1"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "card-1", "task.json"), "not json");
            AgentStudioImportEvent? observed = null;
            var importer = new AgentStudioTaskStorageImporter();
            importer.EventOccurred += importEvent => observed = importEvent;

            Assert.ThrowsAny<System.Text.Json.JsonException>(() => importer.ImportDirectory(directory, new InMemoryAgentStudioRunStore()));

            Assert.NotNull(observed);
            Assert.Equal("agent_studio.task_storage.import_failed", observed!.Name);
            Assert.Equal("JsonReaderException", observed.Context["errorType"]);
            Assert.Equal(Path.Combine(directory, "card-1", "task.json"), observed.Context["path"]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Parse_RetainsExecutionDateUsedForHistoricalCost()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            { "id":"card-8", "model":"claude-sonnet-5", "completedAt":"2026-08-31T23:59:59Z",
              "updatedAt":"2026-09-02T12:00:00Z", "tokenSummary": { "inputTokens":1000000 } }
            """);

        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);

        Assert.Equal(new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc), record.ExecutedAtUtc);
        Assert.Equal(2.00m, record.CostEstimate); // introductory rate at execution, not the later update's $3 rate
    }
}
