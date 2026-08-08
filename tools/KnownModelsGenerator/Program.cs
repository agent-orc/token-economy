using System.Text;
using TokenEconomy;
using TokenEconomy.Tools;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/KnownModelsGenerator -- <output.cs>");
    return 2;
}

var outputPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var source = KnownModelsSourceGenerator.Render(ModelPriceCatalog.Default.Listings.Select(listing => listing.ModelId));
File.WriteAllText(outputPath, source, new UTF8Encoding(false));
Console.WriteLine(outputPath);
return 0;
