var examples = new (string Value, bool Expected)[]
{
    ("A man, a plan, a canal: Panama!", true),
    ("Never odd or even", true),
    ("token economy", false),
    ("...", true),
    ("ab", false),
};

foreach (var example in examples)
    if (StringTools.IsPalindrome(example.Value) != example.Expected)
    {
        Console.Error.WriteLine($"Failed: {example.Value}");
        return 1;
    }
return 0;
