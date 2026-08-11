using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TokenEconomy;

if (args.Length != 2
    || !DateTime.TryParseExact(
        args[0],
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out var asOfUtc))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/WebsiteMatrixDataExporter -- "
        + "<yyyy-MM-ddTHH:mm:ssZ> <output.json>");
    return 2;
}

var rows = ModelEfficiencyMatrix.Default.Describe(asOfUtc).Select(row => new
{
    row.ModelId,
    row.Vendor,
    row.Cli,
    row.Tier,
    row.CostClass,
    row.EffortLevels,
    row.Suitability,
    row.ReviewQuality,
    row.Restricted,
    row.Deprecated,
    row.CostUnconfirmed,
    SelectionStatus = row.RoutingStatus,
    row.EvidenceStatus,
    row.Provisional,
    row.Note,
});
var payload = new
{
    schemaVersion = 1,
    asOfUtc,
    generatedFrom = "ModelEfficiencyMatrix.Default.Describe(asOfUtc)",
    referenceUsage = EfficiencyPolicy.CostReferenceUsage,
    rows,
};
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
};
json.Converters.Add(new JsonStringEnumConverter());

var outputPath = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, json) + "\n", new UTF8Encoding(false));
return 0;
