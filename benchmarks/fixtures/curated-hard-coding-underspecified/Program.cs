var examples = new (string? Configured, string? Environment, string Expected)[]
{
    ("https://configured.test", "https://environment.test", "https://configured.test"),
    (null, " https://environment.test/v1 ", "https://environment.test/v1"),
    ("   ", "https://environment.test", "https://environment.test"),
    (null, "\t", EndpointResolver.DefaultEndpoint),
    (null, null, EndpointResolver.DefaultEndpoint),
};

foreach (var example in examples)
{
    var actual = EndpointResolver.Resolve(example.Configured, example.Environment);
    if (actual != example.Expected)
    {
        Console.Error.WriteLine($"Resolved '{actual}' instead of '{example.Expected}'.");
        return 1;
    }
}

return 0;
