using System.Reflection;
using System.Text.Json;

namespace TokenEconomy;

/// <summary>Loads the versioned, repository-owned default price catalog.</summary>
internal static class ModelPriceCatalogData
{
    private const string ResourceName = "TokenEconomy.catalog.model-prices.json";

    public static IReadOnlyList<ModelListing> LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded pricing catalog '{ResourceName}' was not found.");

        return JsonSerializer.Deserialize<List<ModelListing>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Embedded pricing catalog contains no listings.");
    }
}
