public static class EnvelopeReader
{
    public static string Read(string wire)
    {
        if (!wire.StartsWith("v2:", StringComparison.Ordinal))
            throw new FormatException("Unknown envelope version.");

        return wire[3..].Replace("\\:", ":", StringComparison.Ordinal);
    }
}
