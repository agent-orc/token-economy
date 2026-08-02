using System.Text;
using TokenEconomy;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ModelRoutingKnowledgeReport -- <output.md>");
    return 2;
}

var outputPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, ModelRoutingKnowledgeRenderer.Render(ModelRoutingKnowledgeBase.Default), new UTF8Encoding(false));
Console.WriteLine(outputPath);
return 0;
