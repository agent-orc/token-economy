namespace TokenEconomy;

/// <summary>A strongly typed model identifier for canonical catalog ids and explicit custom ids.</summary>
public readonly record struct ModelId
{
    private readonly string? _value;

    private ModelId(string value) => _value = value;

    /// <summary>The wrapped model identifier.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Create a typed model identifier from an explicit string value.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public static ModelId Of(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ModelId(value);
    }

    /// <summary>Convert a typed model identifier to the string accepted by existing APIs.</summary>
    public static implicit operator string(ModelId modelId) => modelId.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
