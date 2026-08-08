public static class EndpointResolver
{
    public const string DefaultEndpoint = "https://api.example.test";

    public static string Resolve(string? configured, string? environment)
        => environment ?? configured ?? DefaultEndpoint;
}
