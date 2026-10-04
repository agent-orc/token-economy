using System.Security.Cryptography;
using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class PriceSnapshotTests
{
    private const string SnapshotDirectory = "src/TokenEconomy/catalog/price-snapshots";

    [Fact]
    public void EverySnapshotIsHashPinnedAndLoadsAsACatalog()
    {
        using var index = ReadIndex();
        foreach (var snapshot in index.RootElement.GetProperty("snapshots").EnumerateArray())
        {
            var bytes = File.ReadAllBytes(SnapshotPath(snapshot.GetProperty("path").GetString()!));
            Assert.Equal(snapshot.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));

            var catalog = new ModelPriceCatalog(Load(bytes));
            Assert.Equal(snapshot.GetProperty("listings").GetInt32(), catalog.Listings.Count);
            Assert.Equal(snapshot.GetProperty("id").GetString(), snapshot.GetProperty("retrievedOn").GetString());
        }
    }

    [Fact]
    public void EmbeddedCatalogIsTheCurrentSnapshot()
    {
        using var index = ReadIndex();
        var current = index.RootElement.GetProperty("current").GetString();
        var snapshot = index.RootElement.GetProperty("snapshots").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == current);

        Assert.Equal(
            File.ReadAllBytes(SnapshotPath(snapshot.GetProperty("path").GetString()!)),
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "src/TokenEconomy/catalog/model-prices.json")));
        Assert.Equal(current, index.RootElement.GetProperty("snapshots").EnumerateArray()
            .Max(item => item.GetProperty("retrievedOn").GetString()));
    }

    [Fact]
    public void September29RefreshAddsSonnet55AndChangesNoSharedRate()
    {
        var previous = Load(File.ReadAllBytes(SnapshotPath("model-prices.2026-09-24.json"))).ToDictionary(item => item.ModelId);
        var current = Load(File.ReadAllBytes(SnapshotPath("model-prices.2026-09-29.json")));

        Assert.Equal([KnownModels.ClaudeSonnet55.Value],
            current.Select(item => item.ModelId).Except(previous.Keys));
        Assert.Empty(previous.Keys.Except(current.Select(item => item.ModelId)));
        foreach (var listing in current.Where(item => previous.ContainsKey(item.ModelId)))
        {
            var before = previous[listing.ModelId];
            Assert.Equal(before.DisplayName, listing.DisplayName);
            Assert.Equal(before.ReleaseDate, listing.ReleaseDate);
            Assert.Equal(Rates(before), Rates(listing));
        }

        Assert.Equal(new DateOnly(2026, 9, 24), previous[KnownModels.ClaudeOpus55.Value].History.Single().VerifiedOn);
        Assert.Equal(new DateOnly(2026, 9, 12), previous[KnownModels.Gpt6Astra.Value].History.Single().VerifiedOn);
    }

    [Fact]
    public void October4RefreshAddsOnlyGpt61Sol()
    {
        var previous = Load(File.ReadAllBytes(SnapshotPath("model-prices.2026-09-29.json"))).ToDictionary(item => item.ModelId);
        var current = Load(File.ReadAllBytes(SnapshotPath("model-prices.2026-10-04.json")));

        Assert.Equal([KnownModels.Gpt61Sol.Value], current.Select(item => item.ModelId).Except(previous.Keys));
        Assert.Empty(previous.Keys.Except(current.Select(item => item.ModelId)));
        foreach (var listing in current.Where(item => previous.ContainsKey(item.ModelId)))
        {
            var before = previous[listing.ModelId];
            Assert.Equal(before.DisplayName, listing.DisplayName);
            Assert.Equal(before.ReleaseDate, listing.ReleaseDate);
            Assert.Equal(Rates(before), Rates(listing));
        }
    }

    private static IEnumerable<string> Rates(ModelListing listing)
        => listing.History.Select(price =>
            $"{price.ValidFrom:O}|{price.InputPerMTok}|{price.CacheReadPerMTok}|{price.CacheWritePerMTok}|{price.OutputPerMTok}|{price.Currency}|{price.Unconfirmed}");

    private static List<ModelListing> Load(byte[] json)
        => JsonSerializer.Deserialize<List<ModelListing>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static JsonDocument ReadIndex()
        => JsonDocument.Parse(File.ReadAllText(SnapshotPath("index.json")));

    private static string SnapshotPath(string fileName)
        => Path.Combine(RepositoryRoot(), SnapshotDirectory, fileName);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
