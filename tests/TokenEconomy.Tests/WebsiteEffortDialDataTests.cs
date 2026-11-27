using System.Text.Json;
using System.Text.RegularExpressions;
using TokenEconomy;
using TokenEconomy.Tools;
using Xunit;

namespace TokenEconomy.Tests;

/// <summary>
/// The Cost Frontier Quality dial reads website/data/effort-dial.json. These tests keep that file equal to the
/// library's own EffortDial answers and enforce the page rules: only current models are offered, every number
/// carries an evidence class, a date and a link, the fleet cohort is anonymised, and the page never uses the
/// words route or routing.
/// </summary>
public class WebsiteEffortDialDataTests
{
    private static readonly Regex ReservedWord = new(@"\brout(e|es|ed|ing)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TaskKey = new(@"\b(?!GPT-)[A-Z][A-Z0-9]{1,9}-\d+\b", RegexOptions.CultureInvariant);

    [Fact]
    [Trait("Category", "Guard")]
    public void CommittedProjectionIsCurrent()
    {
        var committed = File.ReadAllText(Path.Combine(RepositoryRoot(), "website", "data", "effort-dial.json")).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(committed == EffortDialSiteData.Render(),
            "website/data/effort-dial.json is stale; run dotnet run --project tools/EffortDialSiteData -- website/data/effort-dial.json");
    }

    [Fact]
    public void PublishedPicksEqualTheLibraryAtEveryPosition()
    {
        using var document = Load();
        var dial = EffortDial.Default;
        var classes = document.RootElement.GetProperty("classes").EnumerateArray().ToArray();
        Assert.Equal(dial.WorkClasses.Count, classes.Length);
        foreach (var workClass in classes)
        {
            var id = workClass.GetProperty("id").GetString()!;
            foreach (var cli in new[] { DialCli.Claude, DialCli.Codex })
            {
                var ladder = workClass.GetProperty("ladders").GetProperty(cli == DialCli.Claude ? "claude" : "codex");
                var segments = ladder.GetProperty("picks").EnumerateArray().ToArray();
                Assert.Equal(0, segments[0].GetProperty("from").GetInt32());
                Assert.Equal(100, segments[^1].GetProperty("to").GetInt32());
                for (var position = 0; position <= 100; position++)
                {
                    var segment = segments.Single(s => s.GetProperty("from").GetInt32() <= position && s.GetProperty("to").GetInt32() >= position);
                    var pick = dial.Pick(position, id, cli);
                    Assert.Equal(pick.Rung.Id, segment.GetProperty("rung").GetString());
                    var rung = ladder.GetProperty("rungs").EnumerateArray().Single(r => r.GetProperty("id").GetString() == pick.Rung.Id);
                    var cost = rung.GetProperty("cost");
                    if (pick.Cost.Status == DialCostStatus.Unknown)
                    {
                        Assert.Equal("unknown", cost.GetProperty("status").GetString());
                        Assert.Equal(JsonValueKind.Null, cost.GetProperty("low").ValueKind);
                    }
                    else
                    {
                        Assert.Equal(pick.Cost.ListUsdLow, cost.GetProperty("low").GetDecimal());
                        Assert.Equal(pick.Cost.ListUsdHigh, cost.GetProperty("high").GetDecimal());
                    }
                }
            }
        }
        // The default position runs the standard rungs the owner decided on 17 Nov 2026.
        Assert.Equal("feature-logic.claude.opus-5-5-medium", Segment(classes, "feature-logic", "claude", 50));
        Assert.Equal("review-aspect.claude.opus-5-5-low", Segment(classes, "review-aspect", "claude", 50));
    }

    [Fact]
    [Trait("Category", "Guard")]
    public void OnlyCurrentModelsAreOffered()
    {
        using var document = Load();
        var root = document.RootElement;
        var catalog = ModelPriceCatalog.Default;
        bool Current(string id) => catalog.Find(id)?.LifecycleStatus == "current";
        var offered = new List<string>();
        foreach (var workClass in root.GetProperty("classes").EnumerateArray())
            foreach (var ladder in workClass.GetProperty("ladders").EnumerateObject())
            {
                offered.AddRange(ladder.Value.GetProperty("rungs").EnumerateArray().Select(r => r.GetProperty("model").GetString()!));
                offered.AddRange(ladder.Value.GetProperty("candidates").EnumerateArray().Select(r => r.GetProperty("model").GetString()!));
            }
        var rows = root.GetProperty("rows");
        foreach (var board in root.GetProperty("creativeBoards").EnumerateArray().Concat(root.GetProperty("ladders").EnumerateArray()))
            offered.AddRange(board.GetProperty("rows").EnumerateArray().Select(id => rows.GetProperty(id.GetString()!).GetProperty("model").GetString()!));
        foreach (var evidence in root.GetProperty("evidence").EnumerateObject())
            offered.AddRange(evidence.Value.GetProperty("rows").EnumerateArray().Select(id => rows.GetProperty(id.GetString()!).GetProperty("model").GetString()!));
        Assert.NotEmpty(offered);
        Assert.All(offered.Where(id => !Current(id)), id => Assert.Fail($"Superseded model offered on the dial page: {id}"));

        // Superseded models appear only as the fleet's measured model and as the cost-driver starting point.
        var allowed = root.GetProperty("fleet").GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("model").GetString()!)
            .Append(root.GetProperty("costDrivers").GetProperty("from").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var model in root.GetProperty("models").EnumerateObject())
        {
            Assert.Equal(Current(model.Name), model.Value.GetProperty("current").GetBoolean());
            if (!Current(model.Name)) Assert.Contains(model.Name, allowed);
        }
        Assert.DoesNotContain(root.GetProperty("models").EnumerateObject(), m => m.Name is "claude-fable-5-1" or "gpt-6-astra");
    }

    [Fact]
    [Trait("Category", "Guard")]
    public void EveryNumberCarriesClassDateAndSource()
    {
        using var document = Load();
        var root = document.RootElement;
        var classes = root.GetProperty("evidenceClasses").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(["F", "L", "M", "P", "V"], classes.Order());
        foreach (var evidence in root.GetProperty("evidence").EnumerateObject())
        {
            Assert.Contains(evidence.Value.GetProperty("cls").GetString(), classes);
            Assert.True(DateOnly.TryParse(evidence.Value.GetProperty("date").GetString(), out _), evidence.Name);
            Assert.NotEmpty(evidence.Value.GetProperty("links").EnumerateArray());
            Assert.All(evidence.Value.GetProperty("links").EnumerateArray(), link => Assert.StartsWith("https://", link.GetProperty("url").GetString()));
        }
        foreach (var row in root.GetProperty("rows").EnumerateObject())
        {
            Assert.Contains(row.Value.GetProperty("cls").GetString(), classes);
            Assert.StartsWith("https://", row.Value.GetProperty("url").GetString());
            Assert.True(DateOnly.TryParse(row.Value.GetProperty("retrieved").GetString(), out _), row.Name);
            Assert.True(DateOnly.TryParse(row.Value.GetProperty("published").GetString(), out _), row.Name);
        }
        foreach (var workClass in root.GetProperty("classes").EnumerateArray())
            foreach (var ladder in workClass.GetProperty("ladders").EnumerateObject())
                foreach (var rung in ladder.Value.GetProperty("rungs").EnumerateArray())
                {
                    var ids = rung.GetProperty("evidence").EnumerateArray().Select(e => e.GetString()!).ToArray();
                    Assert.NotEmpty(ids);
                    Assert.All(ids, id => Assert.True(root.GetProperty("evidence").TryGetProperty(id, out _), id));
                }
        foreach (var statement in root.GetProperty("subscriptions").GetProperty("statements").EnumerateArray())
        {
            Assert.Equal("V", statement.GetProperty("cls").GetString());
            Assert.False(string.IsNullOrWhiteSpace(statement.GetProperty("quote").GetString()));
            Assert.Equal("2026-11-17", statement.GetProperty("retrieved").GetString());
            Assert.StartsWith("https://", statement.GetProperty("url").GetString());
        }
        foreach (var model in root.GetProperty("models").EnumerateObject().Where(m => m.Value.GetProperty("input").ValueKind != JsonValueKind.Null))
        {
            Assert.StartsWith("https://", model.Value.GetProperty("priceUrl").GetString());
            Assert.True(DateOnly.TryParse(model.Value.GetProperty("priceDate").GetString(), out _), model.Name);
        }
    }

    [Fact]
    public void CreativeEvidenceShowsIntervalsLimitationsAndMissingRows()
    {
        using var document = Load();
        var root = document.RootElement;
        var boards = root.GetProperty("creativeBoards").EnumerateArray().ToArray();
        var aa = boards[0];
        Assert.StartsWith("aa-3d", aa.GetProperty("bench").GetString());
        Assert.True(aa.GetProperty("excludesBrokenBuilds").GetBoolean());
        Assert.Equal("excluded", aa.GetProperty("dimensions").GetProperty("technicalExecution").GetString());
        Assert.StartsWith("https://artificialanalysis.ai/3d/explorer", aa.GetProperty("explorer").GetString());
        var rows = root.GetProperty("rows");
        var aaRows = aa.GetProperty("rows").EnumerateArray().Select(id => rows.GetProperty(id.GetString()!)).ToArray();
        var sonnet = Assert.Single(aaRows, r => r.GetProperty("model").GetString() == "claude-sonnet-5-5");
        Assert.Equal("max", sonnet.GetProperty("effort").GetString());
        Assert.Equal(1203.69m, sonnet.GetProperty("score").GetDecimal());
        Assert.Equal(28.0m, sonnet.GetProperty("ci").GetDecimal());
        Assert.DoesNotContain(aaRows, r => r.GetProperty("model").GetString() == "claude-sonnet-5-5" && r.GetProperty("effort").GetString() == "high");
        Assert.DoesNotContain(aaRows, r => r.GetProperty("model").GetString() == "claude-opus-5-5" && r.GetProperty("effort").GetString() == "low");

        var webdev = boards[1];
        Assert.Equal("arena-code-webdev-2026-11-23", webdev.GetProperty("bench").GetString());
        Assert.False(webdev.GetProperty("excludesBrokenBuilds").GetBoolean());
        Assert.Equal("none", webdev.GetProperty("publishesCost").GetString());
        var webdevRows = webdev.GetProperty("rows").EnumerateArray().Select(id => rows.GetProperty(id.GetString()!)).ToArray();
        Assert.Contains(webdevRows, r => r.GetProperty("model").GetString() == "claude-sonnet-5-5" && r.GetProperty("effort").GetString() == "high");
        Assert.All(webdevRows, r => Assert.False(r.TryGetProperty("cost", out _)));

        var creative = root.GetProperty("classes").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "creative");
        Assert.Equal("creative", creative.GetProperty("basis").GetString());
        foreach (var rung in creative.GetProperty("ladders").GetProperty("claude").GetProperty("rungs").EnumerateArray())
            foreach (var dimension in new[] { "originality", "design", "gameFeel", "technicalExecution" })
                Assert.False(string.IsNullOrWhiteSpace(rung.GetProperty("dimensions").GetProperty(dimension).GetProperty("text").GetString()));
        var coding = root.GetProperty("conclusions").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "sonnet-not-cheap-opus-on-coding");
        Assert.Contains("creative", coding.GetProperty("doesNotApplyTo").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public void CostDriverAndFleetMatchTheCatalogues()
    {
        using var document = Load();
        var root = document.RootElement;
        var drivers = root.GetProperty("costDrivers");
        Assert.Equal(0.74m, drivers.GetProperty("cacheShare").GetDecimal());
        Assert.Equal(0.63m, drivers.GetProperty("factor").GetDecimal());
        Assert.Equal(10.8m, drivers.GetProperty("anchorUsd").GetDecimal());
        Assert.Equal(EffortDial.Default.Pick(100, "feature-logic", DialCli.Codex).Cost.ListUsdLow, drivers.GetProperty("repricedUsd").GetDecimal());

        var fleet = root.GetProperty("fleet");
        Assert.Equal("F", fleet.GetProperty("cls").GetString());
        Assert.Equal("2026-10-20", fleet.GetProperty("windowStart").GetString());
        Assert.Equal("2026-11-17", fleet.GetProperty("windowEnd").GetString());
        Assert.Equal(28, fleet.GetProperty("days").GetInt32());
        Assert.Equal(471, fleet.GetProperty("rows").EnumerateArray().Sum(r => r.GetProperty("cards").GetInt32()));
        var text = fleet.GetRawText();
        Assert.DoesNotMatch(TaskKey, text.Replace("GPT-6", "", StringComparison.Ordinal));
        Assert.DoesNotContain("Agent Studio", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Guard")]
    public void PageTextNeverSaysRouteOrRouting()
    {
        var root = RepositoryRoot();
        var html = File.ReadAllText(Path.Combine(root, "website", "cost-frontier", "index.html"));
        var start = html.IndexOf("<!-- site-navigation:start -->", StringComparison.Ordinal);
        var end = html.IndexOf("<!-- site-navigation:end -->", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        // The shared site header links to other pages by URL; everything this page writes itself is checked.
        Assert.DoesNotMatch(ReservedWord, html[..start] + html[end..]);
        foreach (var script in new[] { "dial.js", "frontier.js" })
            Assert.DoesNotMatch(ReservedWord, File.ReadAllText(Path.Combine(root, "website", "cost-frontier", script)));
        using var dial = Load();
        Assert.All(Strings(dial.RootElement), text => Assert.DoesNotMatch(ReservedWord, text));
        using var frontier = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "website", "data", "cost-frontier.json")));
        var shown = frontier.RootElement;
        foreach (var property in new[] { "categories", "insights", "missions", "releases", "restricted", "evidence" })
            Assert.All(Strings(shown.GetProperty(property)), text => Assert.DoesNotMatch(ReservedWord, text));
        foreach (var benchmark in shown.GetProperty("benchmarks").EnumerateObject())
            foreach (var field in new[] { "name", "short", "what", "notes", "costMethod", "unit", "yLabel" })
                if (benchmark.Value.TryGetProperty(field, out var value))
                    Assert.All(Strings(value), text => Assert.DoesNotMatch(ReservedWord, text));
    }

    private static string Segment(JsonElement[] classes, string id, string cli, int position) =>
        classes.Single(c => c.GetProperty("id").GetString() == id).GetProperty("ladders").GetProperty(cli).GetProperty("picks")
            .EnumerateArray().Single(s => s.GetProperty("from").GetInt32() <= position && s.GetProperty("to").GetInt32() >= position)
            .GetProperty("rung").GetString()!;

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
        _ => [],
    };

    private static JsonDocument Load() =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepositoryRoot(), "website", "data", "effort-dial.json")));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TokenEconomy.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
