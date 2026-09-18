using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#pragma warning disable CS1591
namespace TokenEconomy;

public sealed record CohortCard(string TaskKey, string Title, string TaskType, string Lane,
    string? Integration, IReadOnlyList<string> Reviews, int CyberRefusals);
public sealed record CohortQuotaObservation(DateTimeOffset At, decimal CodexWeeklyPercent, decimal ClaudeWeeklyPercent);
public sealed record CohortQuotaInterval(DateTimeOffset From, DateTimeOffset Through,
    decimal? CodexPercentagePoints, decimal? ClaudePercentagePoints);
public sealed record CohortAvailabilityFact(string Model, string ErrorClass, int EventCount,
    DateTime? LastSeenUtc, DateTime ObservedAtUtc, decimal? RunRate, string Detail,
    IReadOnlyList<string> TaskKeys);
public sealed record CohortPriceCheck(string TaskKey, int Run, decimal SuppliedCorrectedUsd,
    decimal? RepricedUsd, decimal? RecordedUsd, bool MatchesWithinRounding);
public sealed record CohortModelSummary(string Model, int RecordedRuns, int CardsWithRuns,
    int CompletedCards, int OpenCards, decimal? TotalRunUsd, decimal? MeanUsdPerRun,
    decimal? CompletedHistoryUsd, decimal? UsdPerCompletedCard, ExpectedRunTokens? MeanTokensPerRun,
    decimal? WeeklyQuotaPercentPerRun, decimal? WeeklyQuotaPercentPerCompletedCard,
    string Confidence);
public sealed record AgentStudioCohort(
    string Organisation, string Source, DateTime ExportedAtUtc, string ArtifactReference, string Sha256,
    IReadOnlyList<string> Caveats, IReadOnlyList<CohortCard> Cards,
    IReadOnlyList<AgentStudioRunRecord> Records, IReadOnlyList<CohortPriceCheck> PriceChecks,
    IReadOnlyList<CohortQuotaObservation> QuotaObservations, IReadOnlyList<CohortAvailabilityFact> Availability);
public sealed record CohortEconomicsEvidence(
    string Source, string ArtifactReference, string Sha256, DateTime ExportedAtUtc,
    IReadOnlyList<string> Caveats, IReadOnlyList<CohortCard> Cards,
    IReadOnlyList<CohortModelSummary> WindowModels, IReadOnlyList<CohortModelSummary> FullHistoryModels,
    IReadOnlyList<CohortPriceCheck> PriceChecks, IReadOnlyList<CohortQuotaObservation> QuotaObservations,
    IReadOnlyList<CohortQuotaInterval> QuotaIntervals, IReadOnlyList<CohortAvailabilityFact> Availability);

/// <summary>Imports the operator's tokenSummary export without inferring per-run levels, review joins or quota attribution.</summary>
public sealed class AgentStudioCohortImporter(ModelPriceCatalog? prices = null)
{
    private readonly ModelPriceCatalog _prices = prices ?? ModelPriceCatalog.Default;

    /// <summary>Offset-free run timestamps require the caller's explicit source timezone.</summary>
    public AgentStudioCohort Import(string json, string artifactReference, TimeZoneInfo sourceTimeZone)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var exported = root.GetProperty("exportedAt").GetDateTimeOffset().UtcDateTime;
        var organisation = Text(root, "organisation");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var cards = new List<CohortCard>();
        var records = new List<AgentStudioRunRecord>();
        var checks = new List<CohortPriceCheck>();
        foreach (var card in root.GetProperty("cards").EnumerateArray())
        {
            var key = Text(card, "taskKey");
            if (cards.Any(c => c.TaskKey == key)) throw new InvalidDataException($"Duplicate card {key}.");
            var lane = Text(card, "lane");
            var fact = new CohortCard(key, Text(card, "title"), Text(card, "taskType"), lane,
                card.GetProperty("integration").GetString(), card.GetProperty("reviews").EnumerateArray().Select(v => v.GetString()!).ToArray(),
                card.GetProperty("cyberRefusals").GetInt32());
            if (fact.CyberRefusals < 0) throw new InvalidDataException("Negative refusal count.");
            cards.Add(fact);
            var index = 0;
            foreach (var run in card.GetProperty("runs").EnumerateArray())
            {
                index++;
                var input = run.GetProperty("in").GetInt64();
                var cached = run.GetProperty("cacheRead").GetInt64();
                var uncached = run.GetProperty("uncachedIn").GetInt64();
                if (input < 0 || cached < 0 || uncached < 0 || uncached !=
                    (run.GetProperty("inputIncludesCached").GetBoolean() ? input - cached : input))
                    throw new InvalidDataException($"Inconsistent input/cache accounting for {key}/{index}.");
                var usage = new TokenUsage(uncached, run.GetProperty("out").GetInt64(), cached, run.GetProperty("cacheWrite").GetInt64());
                if (usage.Output < 0 || usage.CacheWrite < 0) throw new InvalidDataException("Negative usage.");
                var rawTimestamp = Text(run, "ts");
                DateTime executed;
                if (DateTime.TryParseExact(rawTimestamp, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                {
                    if (sourceTimeZone.IsInvalidTime(local) || sourceTimeZone.IsAmbiguousTime(local))
                        throw new InvalidDataException($"Ambiguous or invalid local timestamp {rawTimestamp}.");
                    executed = TimeZoneInfo.ConvertTimeToUtc(local, sourceTimeZone);
                }
                else executed = DateTimeOffset.Parse(rawTimestamp, CultureInfo.InvariantCulture).UtcDateTime;
                if (executed > exported) throw new InvalidDataException("Run occurs after export.");
                var model = Text(run, "model");
                var cost = _prices.ComputeCost(model, usage, executed);
                var supplied = run.GetProperty("usdCorrected").GetDecimal();
                checks.Add(new(key, index, supplied, cost.Total,
                    run.TryGetProperty("usdRecordedByAgentStudio", out var recorded) ? recorded.GetDecimal() : null,
                    cost.Total is { } total && Math.Abs(total - supplied) <= .005m));
                records.Add(new()
                {
                    TaskKey = key, Run = index, Project = "agent-studio-reference", OrganisationId = organisation,
                    Model = model, ThinkingLevel = null, RouteGranularity = AgentStudioRouteGranularity.Attempt,
                    TaskType = fact.TaskType, TaskPrompt = fact.Title, FinalLane = lane,
                    CardOutcome = lane == "6-completed" ? OutcomeQualitySignal.Successful : OutcomeQualitySignal.Unknown,
                    Outcome = OutcomeQualitySignal.Unknown, Usage = usage, TokenUsageAvailable = true,
                    ExecutedAtUtc = executed, ObservedAtUtc = exported, CostEstimate = cost.Total,
                    CostStatus = cost.Status, Currency = cost.Price?.Currency, CostUnconfirmed = cost.Unconfirmed,
                    CostCaveat = ModelPrice.EstimatedListPricesCaveat,
                    ProvenanceReference = $"{artifactReference}#/cards/{cards.Count - 1}/runs/{index - 1}", ProvenanceSha256 = sha,
                });
            }
        }
        var probes = root.GetProperty("quotaObservations").EnumerateArray().Select(p => new CohortQuotaObservation(
            p.GetProperty("ts").GetDateTimeOffset(), p.GetProperty("codexWeeklyPct").GetDecimal(), p.GetProperty("claudeWeeklyPct").GetDecimal())).ToArray();
        if (probes.Any(p => p.At.UtcDateTime > exported || p.CodexWeeklyPercent is < 0 or > 100 || p.ClaudeWeeklyPercent is < 0 or > 100)
            || probes.Zip(probes.Skip(1)).Any(p => p.First.At >= p.Second.At))
            throw new InvalidDataException("Invalid quota observations.");
        var refusals = cards.Where(c => c.CyberRefusals > 0).ToArray();
        var availability = refusals.Length == 0 ? Array.Empty<CohortAvailabilityFact>() : [new CohortAvailabilityFact(
            "gpt-6-astra", "unsupported_parameter", refusals.Sum(c => c.CyberRefusals), null, exported, null,
            "access_programs.cyber is not enabled for this organization; model attribution from operator continuation. Counts are turn.failed events, not uniquely identified runs; run denominator, event timestamps and spend are unknown.",
            refusals.Select(c => c.TaskKey).ToArray())];
        return new(organisation, Text(root, "source"), exported, artifactReference, sha,
            root.GetProperty("caveats").EnumerateArray().Select(c => c.GetString()!).Concat([
                $"Offset-free run timestamps interpreted in {sourceTimeZone.Id}; this timezone is a caller assumption, not an exported field.",
                "Run numbers are tokenSummary entry ordinals, not proof that all attempts were recorded. Reviews are card-level facts with no reliable attempt join.",
                "Model summaries pool unknown reasoning levels; they cannot rank specific levels. Open and mixed-model histories are excluded from completed-card costs.",
                "Quota changes are account-wide percentage-point deltas; no reset identity or exclusive run attribution was exported, so per-model/run/card quota costs remain unknown."
            ]).ToArray(), cards, records, checks, probes, availability);
    }

    public CohortEconomicsEvidence Summarize(AgentStudioCohort cohort, DateTime sinceUtc, DateTime asOfUtc)
    {
        if (sinceUtc.Kind != DateTimeKind.Utc || asOfUtc.Kind != DateTimeKind.Utc || sinceUtc > asOfUtc || asOfUtc < cohort.ExportedAtUtc)
            throw new ArgumentException("The UTC evidence window must include the export observation.");
        var intervals = cohort.QuotaObservations.Zip(cohort.QuotaObservations.Skip(1)).Select(p => new CohortQuotaInterval(
            p.First.At, p.Second.At,
            p.Second.CodexWeeklyPercent >= p.First.CodexWeeklyPercent ? p.Second.CodexWeeklyPercent - p.First.CodexWeeklyPercent : null,
            p.Second.ClaudeWeeklyPercent >= p.First.ClaudeWeeklyPercent ? p.Second.ClaudeWeeklyPercent - p.First.ClaudeWeeklyPercent : null)).ToArray();
        return new(cohort.Source, cohort.ArtifactReference, cohort.Sha256, cohort.ExportedAtUtc, cohort.Caveats, cohort.Cards,
            Models(cohort, sinceUtc, asOfUtc), Models(cohort, DateTime.MinValue, asOfUtc), cohort.PriceChecks,
            cohort.QuotaObservations, intervals, cohort.Availability);
    }

    private IReadOnlyList<CohortModelSummary> Models(AgentStudioCohort cohort, DateTime since, DateTime through)
        => cohort.Records.Select(r => r.Model!).Distinct().Order(StringComparer.Ordinal).Select(model =>
        {
            var runs = cohort.Records.Where(r => r.Model == model && r.ExecutedAtUtc >= since && r.ExecutedAtUtc <= through).ToArray();
            var groups = cohort.Records.GroupBy(r => r.TaskKey).Where(g => g.Any(r => runs.Contains(r))).ToArray();
            var completed = groups.Where(g => g.All(r => r.Model == model && r.ExecutedAtUtc >= since && r.ExecutedAtUtc <= through)
                && g.First().CardOutcome == OutcomeQualitySignal.Successful
                && cohort.Cards.Single(c => c.TaskKey == g.Key).CyberRefusals == 0).ToArray();
            decimal? Cost(IEnumerable<AgentStudioRunRecord> records)
            {
                var costs = records.Select(r => _prices.ComputeCost(r.Model, r.Usage, r.ExecutedAtUtc)).ToArray();
                return costs.Length > 0 && costs.All(c => c.Total is not null && c.Price?.Currency == Currencies.Usd)
                    ? costs.Sum(c => c.Total!.Value) : null;
            }
            var total = Cost(runs);
            var completedCost = Cost(completed.SelectMany(g => g));
            return new CohortModelSummary(model, runs.Length, groups.Length, completed.Length,
                groups.Count(g => g.First().CardOutcome != OutcomeQualitySignal.Successful), total,
                runs.Length > 0 ? total / runs.Length : null, completedCost,
                completed.Length > 0 ? completedCost / completed.Length : null,
                runs.Length > 0 ? new(runs.Average(r => (decimal)r.Usage.Input), runs.Average(r => (decimal)r.Usage.CacheRead),
                    runs.Average(r => (decimal)r.Usage.CacheWrite), runs.Average(r => (decimal)r.Usage.Output)) : null,
                null, null, "small observational cohort; unknown levels; completed-history mean is conditional on completion, not a forecast; open spend excluded");
        }).ToArray();

    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString()
        ?? throw new InvalidDataException($"Missing {key}.");
}
