public static class RangePaginator
{
    public static IReadOnlyList<int> Page(int totalItems, int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalItems);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        if (offset >= totalItems) return [];

        var endInclusive = Math.Min(totalItems - 1, offset + limit);
        return Enumerable.Range(offset, endInclusive - offset).ToArray();
    }
}
