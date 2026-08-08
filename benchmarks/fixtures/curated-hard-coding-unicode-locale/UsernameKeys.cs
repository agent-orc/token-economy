public static class UsernameKeys
{
    public static bool AreEquivalent(string left, string right)
        => left.ToLower() == right.ToLower();
}
