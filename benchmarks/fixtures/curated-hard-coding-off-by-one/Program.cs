var examples = new (int Total, int Offset, int Limit, int[] Expected)[]
{
    (0, 0, 3, []),
    (5, 0, 2, [0, 1]),
    (5, 2, 2, [2, 3]),
    (5, 4, 2, [4]),
    (5, 5, 2, []),
};

foreach (var example in examples)
{
    var actual = RangePaginator.Page(example.Total, example.Offset, example.Limit);
    if (!actual.SequenceEqual(example.Expected))
    {
        Console.Error.WriteLine(
            $"Page({example.Total}, {example.Offset}, {example.Limit}) returned [{string.Join(", ", actual)}].");
        return 1;
    }
}

try
{
    RangePaginator.Page(3, 0, 0);
    Console.Error.WriteLine("A zero page size must be rejected.");
    return 1;
}
catch (ArgumentOutOfRangeException)
{
    return 0;
}
