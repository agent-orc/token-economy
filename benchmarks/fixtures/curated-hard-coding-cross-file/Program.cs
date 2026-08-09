var expected = @"v2:C\:\\temp\:north";
var payload = @"C:\temp:north";

if (EnvelopeWriter.Write(payload) != expected)
{
    Console.Error.WriteLine("The writer did not emit the exact v2 escaped representation.");
    return 1;
}

if (EnvelopeReader.Read(expected) != payload)
{
    Console.Error.WriteLine("The reader did not decode an independently supplied v2 envelope.");
    return 1;
}

foreach (var value in new[] { "plain", "north:west", @"C:\temp", @"slash\:colon" })
    if (EnvelopeReader.Read(EnvelopeWriter.Write(value)) != value)
    {
        Console.Error.WriteLine($"Round trip failed for '{value}'.");
        return 1;
    }

try
{
    EnvelopeReader.Read("v1:legacy");
    Console.Error.WriteLine("The reader accepted a legacy envelope.");
    return 1;
}
catch (FormatException)
{
    return 0;
}
