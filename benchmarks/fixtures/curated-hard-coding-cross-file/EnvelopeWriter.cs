public static class EnvelopeWriter
{
    public static string Write(string payload)
    {
        var wire = new System.Text.StringBuilder("v2:");
        foreach (var character in payload)
        {
            if (character is ':' or '\\') wire.Append('\\');
            wire.Append(character);
        }
        return wire.ToString();
    }
}
