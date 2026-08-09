using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;

var examples = new (string Left, string Right, bool Expected)[]
{
    ("FILE", "file", true),
    ("CAFÉ", "CAFE\u0301", true),
    ("Zoë", "ZOË", true),
    ("résumé", "resume", false),
};

foreach (var example in examples)
    if (UsernameKeys.AreEquivalent(example.Left, example.Right) != example.Expected)
    {
        Console.Error.WriteLine($"Unexpected comparison result for '{example.Left}' and '{example.Right}'.");
        return 1;
    }

return 0;
