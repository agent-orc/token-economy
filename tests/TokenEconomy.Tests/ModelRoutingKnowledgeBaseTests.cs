using System.Security.Cryptography;
using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelRoutingKnowledgeBaseTests
{
    private static readonly ModelRoutingKnowledgeBase Knowledge = ModelRoutingKnowledgeBase.Default;

    [Fact]
    public void AuthoritySnapshot_HashAndVersionRemainSynchronized()
    {
        var root = RepositoryRoot();
        var authorityPath = Path.Combine(root, Knowledge.Authority.RepositoryPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(authorityPath)));
        var authority = File.ReadAllText(authorityPath);

        Assert.Equal("2026-07-24", Knowledge.PolicyVersion.ToString("yyyy-MM-dd"));
        Assert.Equal(Knowledge.Authority.ContentSha256, hash);
        Assert.Contains($"Version: {Knowledge.PolicyVersion:yyyy-MM-dd}", authority);
        Assert.Contains("Quota and cost never lower a hard floor.", authority);

        foreach (var route in Knowledge.Routes)
        {
            Assert.Contains($"`{route.ModelId}` / `{route.ThinkingLevel}`", authority);
            if (route.WorkflowRole == RoutingWorkflowRole.CoreTask)
                Assert.Contains($"| `{route.MinimumScore}-{route.MaximumScore}` | {route.Label} |", authority);
        }

        var criterionLabels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["correctnessRisk"] = "Correctness risk",
            ["expectedScope"] = "Expected scope",
            ["contextDemand"] = "Context demand",
            ["taskTypeAndUncertainty"] = "Task type and uncertainty",
            ["empiricalConfidence"] = "Empirical confidence",
            ["quotaAndCostHeadroom"] = "Quota and cost headroom",
        };
        foreach (var criterion in Knowledge.ScoringCriteria)
            Assert.Contains($"| {criterionLabels[criterion.Id]} | {criterion.MaximumPoints} |", authority);

        Assert.Contains("require Sol/xhigh", authority);
        Assert.Contains("requires at least Sol/medium", authority);
        Assert.Contains("requires at least Terra/medium", authority);
        Assert.Contains($"sets empirical confidence to `{Knowledge.ReissueRules.EmpiricalConfidencePointsAfterSemanticFailure}`", authority);
        Assert.Contains("After two semantic failures at the stronger tier", authority);
    }

    [Fact]
    public void GeneratedPublicView_IsDeterministicEvidenceDatedAndCurrent()
    {
        var expected = File.ReadAllText(Path.Combine(RepositoryRoot(), Knowledge.CatalogContracts.GeneratedPublicView))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var first = ModelRoutingKnowledgeRenderer.Render(Knowledge);
        var second = ModelRoutingKnowledgeRenderer.Render(Knowledge);

        Assert.Equal(first, second);
        Assert.Equal(expected, first);
        Assert.Contains($"Evidence as of: `{Knowledge.EvidenceAsOfDate:yyyy-MM-dd}`", first);
    }

    [Fact]
    public void EveryPriceCatalogModel_HasExactlyOneRoutingKnowledgeRecord()
    {
        var priceIds = ModelPriceCatalog.Default.Listings.Select(listing => listing.ModelId).Order(StringComparer.Ordinal);
        var policyPriceIds = Knowledge.Models.Select(model => model.PriceCatalogId).Order(StringComparer.Ordinal);

        Assert.Equal(priceIds, policyPriceIds);
    }

    [Fact]
    public void EverySelectableModelAndSupportedThinkingLevel_ResolvesCanonically()
    {
        foreach (var model in Knowledge.Models.Where(model => model.RoutingStatus is ModelRoutingStatus.Selectable or ModelRoutingStatus.FallbackOnly))
        {
            foreach (var level in model.SupportedThinkingLevels)
            {
                var resolution = Knowledge.Resolve(model.CanonicalId, level);
                Assert.True(resolution.IsResolved, resolution.Reason);
                Assert.Equal(model.CanonicalId, resolution.Model!.CanonicalId);
                Assert.Equal(level, resolution.ThinkingLevel!.Id);
            }

            foreach (var alias in model.Aliases)
                Assert.Equal(model.CanonicalId, Knowledge.FindModel(alias)!.CanonicalId);
        }

        Assert.Equal("gpt-5.6-sol", Knowledge.FindModel("GPT-5-6")!.CanonicalId);
    }

    [Theory]
    [InlineData("no-such-model", "medium", ModelRouteResolutionStatus.UnknownModel)]
    [InlineData("gpt-5.6-sol", "impossible", ModelRouteResolutionStatus.UnknownThinkingLevel)]
    [InlineData("gpt-5.4-mini", "xhigh", ModelRouteResolutionStatus.UnsupportedThinkingLevel)]
    [InlineData("claude-opus-5", "high", ModelRouteResolutionStatus.UnsupportedModel)]
    [InlineData("claude-opus-4-1", "high", ModelRouteResolutionStatus.DeprecatedModel)]
    public void NonSelectableAndUnknownFacts_RemainExplicit(string model, string thinking, ModelRouteResolutionStatus expected)
        => Assert.Equal(expected, Knowledge.Resolve(model, thinking).Status);

    [Fact]
    public void MiniRoleException_CannotResolveAsACoreTask()
        => Assert.Equal(
            ModelRouteResolutionStatus.WorkflowRoleMismatch,
            Knowledge.Resolve("mini", "high", RoutingWorkflowRole.CoreTask).Status);

    [Fact]
    public void ProviderMediaAndTrustLinks_AreResolvableWithoutInventingEvidence()
    {
        var ledger = new ModelTrustLedger();
        Assert.Equal("unverifiedNoDenominator", Knowledge.CatalogContracts.NamedModelTrustEvidenceStatus);
        Assert.Equal(HistoricalModelTrustEvidence.UnattributedModelId, Knowledge.CatalogContracts.HistoricalIncidentAttribution);
        foreach (var model in Knowledge.Models)
        {
            Assert.Equal(Enum.GetValues<MediaCapability>().Length, MediaCapabilityCatalog.Default.Pull(model.CliId, model.MediaCatalogModelId).Count);
            var trust = ledger.Assess(model.TrustModelId);
            Assert.Equal(TrustLevel.Unverified, trust.Level);
            Assert.Null(trust.ViolationRate);
        }
    }

    [Fact]
    public void ProviderFallback_IsExplicitlyScopedAndCannotReplaceTheCorrectnessCriticalFloor()
    {
        var fallback = Assert.Single(Knowledge.ProviderFallbacks);
        Assert.Equal("claude-sonnet-5", fallback.ModelId);
        Assert.Equal("high", fallback.ThinkingLevel);
        Assert.NotEmpty(Knowledge.FallbacksFor("sol-medium"));
        Assert.Empty(Knowledge.FallbacksFor("sol-xhigh"));
        Assert.Contains("sol-xhigh", fallback.NotForRouteIds);
        Assert.True(fallback.Provisional);
    }

    [Fact]
    public void MachineDocument_DeclaresAndMatchesItsSchema()
    {
        var root = RepositoryRoot();
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src/TokenEconomy/catalog/model-routing-policy.json")));
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src/TokenEconomy/catalog/model-routing-policy.schema.json")));

        Assert.Equal("model-routing-policy.schema.json", policy.RootElement.GetProperty("$schema").GetString());
        Assert.Equal(schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32(), Knowledge.SchemaVersion);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
