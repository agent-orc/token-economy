namespace TokenEconomy;

/// <summary>
/// Historical incident records recovered from project decision journals and escalation records.
/// Attribution is deliberately preserved as unknown when the source does not name a model or CLI.
/// </summary>
public static class HistoricalModelTrustEvidence
{
    /// <summary>Stable bucket for source records that cannot honestly be attributed to a model.</summary>
    public const string UnattributedModelId = "unattributed";

    /// <summary>
    /// Incidents known from the July 2026 journals. These are resolved historical violations, not a
    /// claim about any particular provider model. The supplied source records provide only month
    /// precision, so the first day is a normalization sentinel rather than an asserted event date.
    /// </summary>
    public static IReadOnlyList<ModelTrustIncident> Incidents { get; } =
    [
        Incident("aip-7-agent-git-violation-2026-07", "agent-git-violation", "decision-journal/AIP-7", IncidentSeverity.Medium),
        Incident("aip-10-agent-git-violation-2026-07", "agent-git-violation", "decision-journal/AIP-10", IncidentSeverity.Medium),
        Incident("te-shared-checkout-collision-2026-07", "shared-checkout-collision", "escalation-records/TE-shared-checkout-collision", IncidentSeverity.Medium),
    ];

    /// <summary>Loads every recovered historical incident into a ledger without implying an unknown denominator.</summary>
    public static void RecordInto(ModelTrustLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        foreach (var incident in Incidents) ledger.RecordIncident(incident);
    }

    private static ModelTrustIncident Incident(string id, string kind, string source, IncidentSeverity severity) => new()
    {
        IncidentId = id, ModelId = UnattributedModelId, Kind = kind, Severity = severity,
        Status = IncidentStatus.Resolved, OccurredAtUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        ArtifactReference = source,
    };
}
