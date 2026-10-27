using System.Reflection;
using System.Text.Json;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public data records mirror the adjacent JSON schemas.

/// <summary>A dated vendor source. An operator observation is never a vendor source.</summary>
public sealed record SubscriptionSource
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string Publisher { get; init; }
    public required DateOnly RetrievedOn { get; init; }
}

public sealed record SubscriptionModelAccess
{
    public required string Mode { get; init; }
    public required IReadOnlyList<string> ModelIds { get; init; }
}

public sealed record SubscriptionUseContext
{
    public required string Status { get; init; }
    public required string Note { get; init; }
}

public sealed record SubscriptionLimit
{
    public required string Window { get; init; }
    public required string Pool { get; init; }
    public required string Unit { get; init; }
    public decimal? Value { get; init; }
    public required string Note { get; init; }
}

/// <summary>Published, dated plan facts. ModelIds are candidates, not an account permission grant.</summary>
public sealed record SubscriptionPlan
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> CliIds { get; init; }
    public decimal? MonthlyUsd { get; init; }
    public required string Billing { get; init; }
    public required SubscriptionModelAccess ModelAccess { get; init; }
    public required IReadOnlyList<SubscriptionLimit> Limits { get; init; }
    public required IReadOnlyList<string> SourceIds { get; init; }
    public required IReadOnlyList<string> TermsSourceIds { get; init; }
    public required SubscriptionUseContext HeadlessUse { get; init; }
    public required SubscriptionUseContext ThirdPartyUse { get; init; }
    public required string Notes { get; init; }
}

public sealed record SubscriptionObservation
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public string? PlanId { get; init; }
    public required string Kind { get; init; }
    public required DateTime ObservedFromUtc { get; init; }
    public required DateTime ObservedToUtc { get; init; }
    public required string Method { get; init; }
    public required string CliId { get; init; }
    public IReadOnlyList<string>? AllowedModelIds { get; init; }
    public IReadOnlyList<string>? RejectedModelIds { get; init; }
    public string? Window { get; init; }
    public IReadOnlyList<DateTime>? ResetAtUtc { get; init; }
    public decimal? PercentagePointsPerHour { get; init; }
    public decimal? PercentagePoints { get; init; }
    public decimal? CorrespondingWeeklyPercentagePoints { get; init; }
    public required string Note { get; init; }
}

/// <summary>One approximate token measurement per reported quota percentage delta.</summary>
public sealed record QuotaTokenObservation
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string PlanId { get; init; }
    public required string CliId { get; init; }
    public required string Window { get; init; }
    public required string Period { get; init; }
    public required decimal PercentagePoints { get; init; }
    public long? TotalTokensApprox { get; init; }
    public long? UncachedInputTokensApprox { get; init; }
    public long? CachedInputTokensApprox { get; init; }
    public long? OutputTokensApprox { get; init; }
    public long? ReasoningTokensApprox { get; init; }
    public required bool OutputIncludesReasoning { get; init; }
    public required string Method { get; init; }
    public required string Completeness { get; init; }
    public required string Note { get; init; }
}

public enum PlanModelStatus { Candidate, Unavailable, Unknown }

public sealed record LoginPlanModel(string ModelId, PlanModelStatus Status, string Evidence);

/// <summary>Per-login view for Agent Studio. The caller supplies a verified plan claim; this API does not inspect credentials.</summary>
public sealed record LoginSubscriptionView(string LoginId, string Provider, string? PlanClaim, string? PlanId,
    DateTime ObservedAtUtc, IReadOnlyList<LoginPlanModel> Models);

public sealed record PlanChangeWarning(string LoginId, string ModelId, string PreviousPlanId,
    string CurrentPlanId, string Reason);

/// <summary>Read-only plan facts and conservative per-login candidate mapping.</summary>
public sealed class SubscriptionCatalogue
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public DateOnly AsOfDate { get; }
    public IReadOnlyList<SubscriptionPlan> Plans { get; }
    public IReadOnlyList<SubscriptionSource> Sources { get; }
    public IReadOnlyList<SubscriptionObservation> Observations { get; }
    public IReadOnlyList<QuotaTokenObservation> QuotaTokenObservations { get; }

    public SubscriptionCatalogue(DateOnly asOfDate, IEnumerable<SubscriptionPlan> plans,
        IEnumerable<SubscriptionSource> sources, IEnumerable<SubscriptionObservation> observations,
        IEnumerable<QuotaTokenObservation> quotaTokenObservations)
    {
        AsOfDate = asOfDate;
        Plans = Freeze(plans.Select(p => p with
        {
            CliIds = Freeze(p.CliIds),
            ModelAccess = p.ModelAccess with { ModelIds = Freeze(p.ModelAccess.ModelIds) },
            Limits = Freeze(p.Limits),
            SourceIds = Freeze(p.SourceIds),
            TermsSourceIds = Freeze(p.TermsSourceIds),
        }));
        Sources = Freeze(sources);
        Observations = Freeze(observations.Select(o => o with
        {
            AllowedModelIds = o.AllowedModelIds is null ? null : Freeze(o.AllowedModelIds),
            RejectedModelIds = o.RejectedModelIds is null ? null : Freeze(o.RejectedModelIds),
            ResetAtUtc = o.ResetAtUtc is null ? null : Freeze(o.ResetAtUtc),
        }));
        QuotaTokenObservations = Freeze(quotaTokenObservations);
        Validate();
    }

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());

    public static SubscriptionCatalogue Default { get; } = LoadDefault();

    private static SubscriptionCatalogue LoadDefault()
    {
        var plans = Read<PlanDocument>("TokenEconomy.catalog.subscription-plans.json");
        var sources = Read<SourceDocument>("TokenEconomy.catalog.source-register.json");
        var observations = Read<ObservationDocument>("TokenEconomy.catalog.subscription-observations.json");
        var quota = Read<QuotaTokenDocument>("TokenEconomy.catalog.quota-token-observations.json");
        if (plans.SchemaVersion != 1 || sources.SchemaVersion != 1 || observations.SchemaVersion != 1 || quota.SchemaVersion != 1)
            throw new InvalidDataException("Unsupported subscription catalogue schema version.");
        return new SubscriptionCatalogue(plans.AsOfDate, plans.Plans, sources.Sources, observations.Observations, quota.Observations);
    }

    private static T Read<T>(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing embedded catalogue: {name}");
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidDataException($"Empty catalogue: {name}");
    }

    public SubscriptionPlan? Find(string planId) => Plans.SingleOrDefault(p => p.Id == planId);

    /// <summary>Use the verified id_token's chatgpt_plan_type claim; the caller validates the token.</summary>
    public LoginSubscriptionView ResolveOpenAiLogin(string loginId, IReadOnlyDictionary<string, string> verifiedClaims,
        DateTime observedAtUtc, IEnumerable<string> requiredModelIds)
    {
        ArgumentNullException.ThrowIfNull(verifiedClaims);
        verifiedClaims.TryGetValue("chatgpt_plan_type", out var planType);
        return ResolveLogin("openai", loginId, planType, observedAtUtc, requiredModelIds);
    }

    /// <summary>Resolve one verified login's plan claim. Unknown claims never grant model access.</summary>
    public LoginSubscriptionView ResolveLogin(string provider, string loginId, string? planClaim,
        DateTime observedAtUtc, IEnumerable<string> requiredModelIds)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(loginId))
            throw new ArgumentException("Provider and login ID are required.");
        if (observedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observed time must be UTC.", nameof(observedAtUtc));
        ArgumentNullException.ThrowIfNull(requiredModelIds);
        var planId = provider.ToLowerInvariant() == "openai" ? planClaim?.ToLowerInvariant() switch
        {
            "free" => "openai-free", "plus" => "openai-plus", "pro" => "openai-pro",
            "team" or "business" => "openai-business", "enterprise" => "openai-enterprise",
            "openai-api" => "openai-api", _ => null,
        } : planClaim;
        var plan = planId is null ? null : Find(planId);
        if (plan?.Provider != provider.ToLowerInvariant()) plan = null;
        var models = requiredModelIds.Distinct(StringComparer.Ordinal).Select(modelId =>
        {
            var status = plan is null || plan.ModelAccess.Mode is "account" or "dynamic" or "automatic"
                ? PlanModelStatus.Unknown
                : plan.ModelAccess.ModelIds.Contains(modelId, StringComparer.Ordinal)
                    ? PlanModelStatus.Candidate : PlanModelStatus.Unavailable;
            var evidence = plan is null ? "unknown-plan" : plan.ModelAccess.Mode is "account" or "dynamic" or "automatic"
                ? "account-specific" : string.Join(",", plan.SourceIds);
            return new LoginPlanModel(modelId, status, evidence);
        }).ToArray();
        return new LoginSubscriptionView(loginId, provider.ToLowerInvariant(), planClaim, plan?.Id, observedAtUtc, Freeze(models));
    }

    /// <summary>Warn only on candidate-to-unavailable regressions; unknown access remains unknown.</summary>
    public IReadOnlyList<PlanChangeWarning> AssessPlanChange(LoginSubscriptionView previous, LoginSubscriptionView current)
    {
        if (previous.LoginId != current.LoginId || previous.Provider != current.Provider)
            throw new ArgumentException("Views must describe the same login and provider.");
        if (current.ObservedAtUtc < previous.ObservedAtUtc)
            throw new ArgumentException("Current view must not precede previous view.");
        if (!previous.Models.Select(m => m.ModelId).ToHashSet().SetEquals(current.Models.Select(m => m.ModelId)))
            throw new ArgumentException("Views must cover the same required models.");
        if (previous.PlanId == current.PlanId || previous.PlanId is null || current.PlanId is null) return [];
        var previousModels = previous.Models.Where(m => m.Status == PlanModelStatus.Candidate).Select(m => m.ModelId).ToHashSet();
        return Freeze(current.Models.Where(m => previousModels.Contains(m.ModelId) && m.Status == PlanModelStatus.Unavailable)
            .Select(m => new PlanChangeWarning(current.LoginId, m.ModelId, previous.PlanId, current.PlanId,
                "Plan change removed this model from the catalogue candidates; verify access before routing."))
            .ToArray());
    }

    private void Validate()
    {
        if (Plans.Count == 0 || Plans.Select(p => p.Id).Distinct().Count() != Plans.Count) throw new ArgumentException("Plans must be nonempty and unique.");
        var sourceIds = Sources.Select(s => s.Id).ToHashSet();
        if (Sources.Select(s => s.Id).Distinct().Count() != Sources.Count || Sources.Any(s =>
                !Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || s.RetrievedOn > AsOfDate || string.IsNullOrWhiteSpace(s.Publisher)))
            throw new ArgumentException("Sources must have unique IDs, HTTPS URLs, publisher and a valid retrieval date.");
        foreach (var plan in Plans)
        {
            if (string.IsNullOrWhiteSpace(plan.Provider) || string.IsNullOrWhiteSpace(plan.Name)
                || plan.MonthlyUsd < 0 || plan.CliIds.Count == 0 || plan.Limits.Count == 0
                || plan.SourceIds.Count == 0 || plan.TermsSourceIds.Count == 0
                || plan.HeadlessUse.Status is not ("documented" or "conditional" or "restricted" or "ambiguous" or "unknown")
                || plan.ThirdPartyUse.Status is not ("documented" or "conditional" or "restricted" or "ambiguous" or "unknown")
                || string.IsNullOrWhiteSpace(plan.HeadlessUse.Note) || string.IsNullOrWhiteSpace(plan.ThirdPartyUse.Note)
                || plan.SourceIds.Concat(plan.TermsSourceIds).Any(id => !sourceIds.Contains(id))
                || plan.ModelAccess.Mode is not ("listed" or "account" or "dynamic" or "automatic")
                || (plan.ModelAccess.Mode == "listed" && plan.ModelAccess.ModelIds.Count == 0)
                || plan.Limits.Any(l => string.IsNullOrWhiteSpace(l.Window) || string.IsNullOrWhiteSpace(l.Pool)
                    || string.IsNullOrWhiteSpace(l.Unit) || l.Value < 0))
                throw new ArgumentException($"Invalid plan or missing source: {plan.Id}");
        }
        if (Observations.Select(o => o.Id).Distinct().Count() != Observations.Count || Observations.Any(o =>
                o.ObservedFromUtc.Kind != DateTimeKind.Utc || o.ObservedToUtc.Kind != DateTimeKind.Utc
                || o.ObservedFromUtc > o.ObservedToUtc || string.IsNullOrWhiteSpace(o.Method)
                || (o.PlanId is not null && Find(o.PlanId) is null)))
            throw new ArgumentException("Invalid operator observation.");
        if (QuotaTokenObservations.Select(o => o.Id).Distinct().Count() != QuotaTokenObservations.Count ||
            QuotaTokenObservations.Any(o => Find(o.PlanId) is null || o.PercentagePoints <= 0
                || string.IsNullOrWhiteSpace(o.Method) || string.IsNullOrWhiteSpace(o.Completeness)
                || new long?[] { o.TotalTokensApprox, o.UncachedInputTokensApprox, o.CachedInputTokensApprox,
                    o.OutputTokensApprox, o.ReasoningTokensApprox }.Any(n => n < 0)))
            throw new ArgumentException("Invalid quota token observation.");
    }

    private sealed record PlanDocument(int SchemaVersion, DateOnly AsOfDate, SubscriptionPlan[] Plans);
    private sealed record SourceDocument(int SchemaVersion, SubscriptionSource[] Sources);
    private sealed record ObservationDocument(int SchemaVersion, SubscriptionObservation[] Observations);
    private sealed record QuotaTokenDocument(int SchemaVersion, QuotaTokenObservation[] Observations);
}
