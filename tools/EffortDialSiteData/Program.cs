using System.Text;
using TokenEconomy.Tools;

// Writes the Cost Frontier dial projection. Run from the repository root:
//   dotnet run --project tools/EffortDialSiteData -- website/data/effort-dial.json
//   dotnet run --project tools/EffortDialSiteData -- website/data/effort-dial.json --check
if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--check"))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/EffortDialSiteData -- <output.json> [--check]");
    return 2;
}

var outputPath = Path.GetFullPath(args[0]);
var rendered = EffortDialSiteData.Render();
if (args.Length == 2)
{
    if (!File.Exists(outputPath) || File.ReadAllText(outputPath, Encoding.UTF8) != rendered)
    {
        Console.Error.WriteLine($"{args[0]} is stale; run dotnet run --project tools/EffortDialSiteData -- {args[0]}");
        return 1;
    }
    Console.WriteLine($"{args[0]} is current.");
    return 0;
}
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, rendered, new UTF8Encoding(false));
Console.WriteLine(outputPath);
return 0;
