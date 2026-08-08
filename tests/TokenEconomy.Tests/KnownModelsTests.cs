using System.Reflection;
using System.Text;
using TokenEconomy;
using TokenEconomy.Tools;
using Xunit;

namespace TokenEconomy.Tests;

public class KnownModelsTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("claude-opus-4-8", "ClaudeOpus48")]
    [InlineData("gpt-5.6-sol", "Gpt56Sol")]
    public void PascalName_SplitsDashesAndDotsAndCapitalizesSegments(string modelId, string expected)
        => Assert.Equal(expected, KnownModelsSourceGenerator.ToPascalName(modelId));

    [Fact]
    public void Generator_RejectsMemberNameCollisions()
    {
        var error = Assert.Throws<ArgumentException>(() => KnownModelsSourceGenerator.Render(
            ["gpt-5.6-sol", "gpt-5-6-sol"]));

        Assert.Contains("KnownModels.Gpt56Sol", error.Message);
    }

    [Fact]
    public void CheckedInKnownModels_IsByteIdenticalToTheCatalogProjection()
    {
        var generated = KnownModelsSourceGenerator.Render(
            ModelPriceCatalog.Default.Listings.Select(listing => listing.ModelId));
        var checkedIn = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "src", "TokenEconomy", "KnownModels.g.cs"));

        Assert.Equal(checkedIn, new UTF8Encoding(false).GetBytes(generated));
    }

    [Fact]
    public void KnownModels_ContainsOneReadonlyFieldPerCanonicalCatalogEntry()
    {
        var fields = typeof(KnownModels).GetFields(BindingFlags.Public | BindingFlags.Static);
        var generatedIds = fields.Select(field => Assert.IsType<ModelId>(field.GetValue(null))).Select(model => model.Value).Order(StringComparer.Ordinal);
        var catalogIds = ModelPriceCatalog.Default.Listings.Select(listing => listing.ModelId).Order(StringComparer.Ordinal);

        Assert.Equal(19, fields.Length);
        Assert.All(fields, field => Assert.True(field.IsInitOnly));
        Assert.Equal(catalogIds, generatedIds);
    }

    [Fact]
    public void ModelId_ConvertsToStringButNotFromString()
    {
        var model = ModelId.Of("custom-model");
        string value = model;

        Assert.Equal("custom-model", value);
        Assert.Equal("custom-model", model.ToString());
        Assert.DoesNotContain(
            typeof(ModelId).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "op_Implicit"
                && method.ReturnType == typeof(ModelId)
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string));
        Assert.Throws<ArgumentException>(() => ModelId.Of("  "));
    }

    [Fact]
    public void TypedCatalogOverloads_ForwardToTheExistingStringBehavior()
    {
        var catalog = ModelPriceCatalog.Default;
        var model = KnownModels.ClaudeSonnet5;
        var usage = new TokenUsage(1_000_000, 200_000);

        Assert.Same(catalog.Find((string)model), catalog.Find(model));
        Assert.Equal(catalog.PriceDevelopment((string)model), catalog.PriceDevelopment(model));
        Assert.Equal(catalog.ResolvePrice((string)model, Now), catalog.ResolvePrice(model, Now));
        Assert.Equal(catalog.ComputeCost((string)model, usage, Now), catalog.ComputeCost(model, usage, Now));
        Assert.Equal(catalog.Cost((string)model, usage.Input, usage.Output, Now), catalog.Cost(model, usage.Input, usage.Output, Now));
    }

    [Fact]
    public void TypedMatrixOverloads_ForwardToTheExistingStringBehavior()
    {
        var matrix = ModelEfficiencyMatrix.Default;
        var model = KnownModels.ClaudeSonnet5;

        Assert.Same(matrix.Find((string)model), matrix.Find(model));
        Assert.Equal(matrix.CliOf((string)model), matrix.CliOf(model));
        Assert.Equal(matrix.CostClassOf((string)model, Now), matrix.CostClassOf(model, Now));
        Assert.Equal(matrix.SuitabilityOf((string)model, TaskClass.Feature), matrix.SuitabilityOf(model, TaskClass.Feature));
        Assert.Equal(
            matrix.EvaluateModel((string)model, TaskClass.Feature, BudgetPressure.Tight, Now),
            matrix.EvaluateModel(model, TaskClass.Feature, BudgetPressure.Tight, Now));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
