namespace TokenEconomy;

/// <summary>
/// The Token Economy evidence classes shared by benchmark rows, price provenance and the effort dial.
/// </summary>
public static class EvidenceClasses
{
    /// <summary>Third-party measurement whose raw data was pulled.</summary>
    public const string Measured = "M";

    /// <summary>Vendor statement.</summary>
    public const string Vendor = "V";

    /// <summary>Derived arithmetically from list prices or rate cards.</summary>
    public const string ListPriceDerivation = "L";

    /// <summary>Press or operator lead not verified at the source.</summary>
    public const string Press = "P";

    /// <summary>
    /// Measured in our fleet (Agent Studio runs). Anonymised: no card keys or project names; a measurement
    /// window and a case count are required.
    /// </summary>
    public const string Fleet = "F";

    /// <summary>All classes in their documented order.</summary>
    public static IReadOnlyList<string> All { get; } = [Measured, Vendor, ListPriceDerivation, Press, Fleet];

    /// <summary>True for one of the documented class identifiers.</summary>
    public static bool IsKnown(string? evidenceClass) => evidenceClass is not null && All.Contains(evidenceClass, StringComparer.Ordinal);
}
