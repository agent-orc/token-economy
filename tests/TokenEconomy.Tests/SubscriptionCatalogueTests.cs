using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class SubscriptionCatalogueTests
{
    private static readonly DateTime Before = new(2026, 10, 19, 23, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime After = new(2026, 10, 20, 8, 9, 0, DateTimeKind.Utc);

    [Fact]
    public void Catalogue_covers_required_providers_plans_and_cli_quota_shapes()
    {
        var catalogue = SubscriptionCatalogue.Default;
        Assert.Equal(new DateOnly(2026, 10, 20), catalogue.AsOfDate);
        Assert.Equal(new[] { "anthropic", "github", "google", "openai" },
            catalogue.Plans.Select(p => p.Provider).Distinct().Order());
        foreach (var id in new[] { "anthropic-pro", "anthropic-max-5x", "anthropic-max-20x",
            "anthropic-team-standard", "anthropic-team-premium", "anthropic-enterprise", "anthropic-api",
            "openai-free", "openai-plus", "openai-pro", "openai-business", "openai-enterprise", "openai-api",
            "google-free", "google-ai-plus", "google-ai-pro", "google-ai-ultra-5x", "google-ai-ultra-20x",
            "google-gemini-cli-free", "google-gemini-cli-pro", "google-gemini-cli-ultra", "google-gemini-api",
            "github-free", "github-pro", "github-pro-plus", "github-max", "github-business", "github-enterprise" })
            Assert.NotNull(catalogue.Find(id));
        Assert.Contains(catalogue.Find("anthropic-max-20x")!.Limits, l => l.Window == "five-hour");
        Assert.Contains(catalogue.Find("anthropic-max-20x")!.Limits, l => l.Window == "weekly");
        Assert.Contains(catalogue.Find("google-ai-pro")!.Limits, l => l.Window == "five-hour");
        Assert.DoesNotContain(catalogue.Find("anthropic-enterprise")!.Limits, l => l.Window == "five-hour");
        Assert.Equal(1500, catalogue.Find("google-gemini-cli-pro")!.Limits.Single().Value);
        Assert.Equal(3900, catalogue.Find("github-pro-plus")!.Limits.Single().Value);
        Assert.Equal("restricted", catalogue.Find("google-ai-pro")!.ThirdPartyUse.Status);
        Assert.Equal("ambiguous", catalogue.Find("anthropic-max-20x")!.ThirdPartyUse.Status);
        Assert.All(catalogue.Plans, p =>
        {
            Assert.NotEmpty(p.SourceIds);
            Assert.NotEmpty(p.TermsSourceIds);
            Assert.Equal("documented", p.HeadlessUse.Status);
            Assert.False(string.IsNullOrWhiteSpace(p.ThirdPartyUse.Note));
            Assert.All(p.SourceIds.Concat(p.TermsSourceIds), id => Assert.Contains(catalogue.Sources, s => s.Id == id));
        });
    }

    [Fact]
    public void Schema_documents_required_fields_and_separates_vendor_facts_from_observations()
    {
        var root = LanguageCapabilityCatalogTests.Root();
        foreach (var filename in new[] { "subscription-plans", "subscription-observations", "quota-token-observations" })
        {
            using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "TokenEconomy", "catalog", filename + ".schema.json")));
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", schema.RootElement.GetProperty("$schema").GetString());
            Assert.Contains("schemaVersion", schema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal(JsonValueKind.False, schema.RootElement.GetProperty("additionalProperties").ValueKind);
        }
        Assert.DoesNotContain(SubscriptionCatalogue.Default.Observations, o => string.IsNullOrWhiteSpace(o.Method));
        Assert.DoesNotContain(SubscriptionCatalogue.Default.Sources, s => s.RetrievedOn > new DateOnly(2026, 10, 20));
        Assert.Contains(SubscriptionCatalogue.Default.Sources, s => s.Id == "openai-plan-models" && s.RetrievedOn == new DateOnly(2026, 10, 20));
    }

    [Fact]
    public void Verified_openai_claim_maps_models_and_warns_on_pro_to_free_regression()
    {
        var catalogue = SubscriptionCatalogue.Default;
        var models = new[] { "gpt-6-luna", "gpt-6-sol", "gpt-6.1-sol", "gpt-5.6-sol" };
        var pro = catalogue.ResolveOpenAiLogin("login-1", new Dictionary<string, string> { ["chatgpt_plan_type"] = "pro" }, Before, models);
        var free = catalogue.ResolveOpenAiLogin("login-1", new Dictionary<string, string> { ["chatgpt_plan_type"] = "free" }, After, models);
        Assert.Equal("openai-pro", pro.PlanId);
        Assert.Equal("openai-free", free.PlanId);
        Assert.All(pro.Models, model => Assert.Equal(PlanModelStatus.Candidate, model.Status));
        Assert.Equal(PlanModelStatus.Candidate, free.Models.Single(x => x.ModelId == "gpt-6-luna").Status);
        Assert.Equal(PlanModelStatus.Unavailable, free.Models.Single(x => x.ModelId == "gpt-6-sol").Status);
        Assert.Equal(new[] { "gpt-5.6-sol", "gpt-6-sol", "gpt-6.1-sol" },
            catalogue.AssessPlanChange(pro, free).Select(w => w.ModelId).Order());
        Assert.Empty(catalogue.AssessPlanChange(free, catalogue.ResolveOpenAiLogin("login-1",
            new Dictionary<string, string> { ["chatgpt_plan_type"] = "pro" }, After.AddMinutes(1), models)));
    }

    [Fact]
    public void Unknown_claim_and_account_dependent_api_key_do_not_grant_models()
    {
        var catalogue = SubscriptionCatalogue.Default;
        var unknown = catalogue.ResolveOpenAiLogin("login-1", new Dictionary<string, string>(), After, ["gpt-6-sol"]);
        Assert.Null(unknown.PlanId);
        Assert.Equal(PlanModelStatus.Unknown, unknown.Models.Single().Status);
        var api = catalogue.ResolveLogin("openai", "api-1", "openai-api", After, ["gpt-6-sol"]);
        Assert.Equal(PlanModelStatus.Unknown, api.Models.Single().Status);
        Assert.Throws<ArgumentException>(() => catalogue.ResolveLogin("openai", "login-1", "free",
            DateTime.SpecifyKind(After, DateTimeKind.Unspecified), ["gpt-6-sol"]));
    }

    [Fact]
    public void Operator_measurements_remain_dated_and_separate_from_published_limits()
    {
        var catalogue = SubscriptionCatalogue.Default;
        var freeProbe = catalogue.Observations.Single(x => x.Id == "codex-free-20261019");
        Assert.Contains("gpt-6-luna", freeProbe.AllowedModelIds!);
        Assert.Contains("gpt-6-sol", freeProbe.RejectedModelIds!);
        Assert.Contains(catalogue.Observations, x => x.Kind == "quota-reset" && x.ResetAtUtc!.Length == 5);
        Assert.Contains(catalogue.Observations, x => x.Kind == "window-ratio" && x.PlanId is null);
        var points = catalogue.QuotaTokenObservations.OrderBy(x => x.Period).ToArray();
        Assert.Equal(3, points.Length);
        Assert.Equal(new long[] { 29_000_000, 48_000_000, 22_000_000 }, points.Select(x => x.TotalTokensApprox!.Value));
        Assert.All(points, x =>
        {
            Assert.Null(x.ReasoningTokensApprox);
            Assert.True(x.OutputIncludesReasoning);
            Assert.Equal(x.TotalTokensApprox, x.UncachedInputTokensApprox + x.CachedInputTokensApprox + x.OutputTokensApprox);
        });
        Assert.Equal("incomplete", points.Last().Completeness);
        Assert.All(catalogue.Find("openai-pro")!.Limits, limit => Assert.Null(limit.Value));
    }

    [Fact]
    public void Invalid_source_and_uncited_plan_are_rejected()
    {
        var catalog = SubscriptionCatalogue.Default;
        Assert.Throws<ArgumentException>(() => new SubscriptionCatalogue(catalog.AsOfDate,
            [catalog.Plans[0] with { SourceIds = ["missing"] }], catalog.Sources, [], []));
        Assert.Throws<ArgumentException>(() => new SubscriptionCatalogue(catalog.AsOfDate,
            [catalog.Plans[0]], [catalog.Sources[0] with { RetrievedOn = new DateOnly(2026, 10, 21) }], [], []));
        Assert.Throws<ArgumentException>(() => new SubscriptionCatalogue(catalog.AsOfDate,
            [catalog.Plans[0] with { MonthlyUsd = -1 }], catalog.Sources, [], []));
    }
}
