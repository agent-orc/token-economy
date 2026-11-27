using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TokenEconomy.Tools;

/// <summary>
/// Projects the effort dial, its evidence and the effort-ladder benchmarks into
/// <c>website/data/effort-dial.json</c> for the Cost Frontier page. Picks and costs come from
/// <see cref="EffortDial.Default"/> for every slider position, so the page cannot drift from the library.
/// Only models whose price-catalogue lifecycle is <c>current</c> are offered as candidates; the
/// superseded models the fleet cohort and the cost-driver explanation measured are marked as such.
/// </summary>
public static partial class EffortDialSiteData
{
    /// <summary>Effort ladders shown on the page (the owner's list), cheapest first within each lab.</summary>
    public static readonly IReadOnlyList<string> LadderModels =
    [
        "claude-haiku-5-5", "claude-sonnet-5-5", "claude-opus-5-5",
        "gpt-6-luna", "gpt-6.1-terra", "gpt-6.1-sol",
    ];

    /// <summary>Benchmarks of the effort-ladder view: (page id, benchmark type id, unit of cost).</summary>
    public static readonly IReadOnlyList<(string Id, string TypeId, string Per)> LadderBenchmarks =
    [
        ("tb41", "terminal-bench-v4.1-official-native-agents", "trial"),
        ("arc2", "arc-agi-2-v2-semi-private-2026-11-17", "task"),
        ("aa5", "cost-frontier-aa5", "task"),
    ];

    /// <summary>Creative boards shown with their rows (AA-3D first: the main evidence for class i).</summary>
    public static readonly IReadOnlyList<string> CreativeBoards =
    [
        "aa-3d-v1.0-2026-11-27", "arena-code-webdev-2026-11-23", "arena-code-webdev-3d-2026-11-23",
        "designarena-3d-design-2026-11-27", "designarena-game-dev-html-2026-11-27",
        "designarena-game-dev-agentic-2026-11-27", "designarena-website-html-2026-11-27",
        "designarena-webapps-agentic-2026-11-27",
    ];

    private const string FleetCacheObservation = "fleet-cache-gpt-6-sol-high";
    private const string AaExplorer = "https://artificialanalysis.ai/3d/explorer";
    private const string DialDocs = "https://github.com/agent-orc/token-economy/blob/main/docs/effort-dial.md";
    private const string CreativeDocs = "https://github.com/agent-orc/token-economy/blob/main/docs/creative-benchmarks-2026-11-27.md";

    public static string Render()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return Build().ToJsonString(options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static JsonObject Build()
    {
        var dial = EffortDial.Default;
        var prices = ModelPriceCatalog.Default;
        var routing = ModelRoutingKnowledgeBase.Default;
        var main = BenchmarkEvidenceCatalog.Default;
        var creative = BenchmarkEvidenceCatalog.Creative;
        var subscriptions = SubscriptionCatalogue.Default;
        var priceAt = dial.AsOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var models = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var rows = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var benches = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);

        bool IsCurrent(string modelId) => prices.Find(modelId)?.LifecycleStatus == "current";

        void AddModel(string modelId)
        {
            if (models.ContainsKey(modelId)) return;
            var listing = prices.Find(modelId) ?? throw new InvalidOperationException($"Unknown model {modelId}");
            // An unpriced model (for example restricted Gemini 4 Carbon) stays unpriced, never zero.
            var price = prices.ResolvePrice(modelId, priceAt).Price;
            models[modelId] = new JsonObject
            {
                ["name"] = listing.DisplayName,
                ["lab"] = listing.Vendor,
                ["current"] = listing.LifecycleStatus == "current",
                ["selectable"] = routing.FindModel(modelId)?.RoutingStatus == ModelRoutingStatus.Selectable,
                ["input"] = price?.InputPerMTok,
                ["cacheRead"] = price?.CacheReadPerMTok,
                ["output"] = price?.OutputPerMTok,
                ["priceDate"] = price is null ? null : Date(price.VerifiedOn ?? DateOnly.FromDateTime(price.ValidFrom)),
                ["priceUrl"] = price?.SourceUrls.FirstOrDefault() ?? listing.ReleaseDateSource,
                ["availability"] = listing.Availability,
            };
        }

        BenchmarkResult Result(string id) =>
            main.FindResult(id) ?? creative.FindResult(id) ?? throw new InvalidOperationException($"Unknown benchmark row {id}");

        void AddBench(string typeId)
        {
            if (benches.ContainsKey(typeId)) return;
            var type = main.FindType(typeId) ?? creative.FindType(typeId) ?? throw new InvalidOperationException($"Unknown benchmark type {typeId}");
            benches[typeId] = new JsonObject
            {
                ["name"] = Clean(type.Name),
                ["version"] = Clean(type.Version),
                ["publisher"] = Clean(type.Publisher),
                ["unit"] = Clean(type.Unit),
                ["methodology"] = type.MethodologyUrl,
                ["date"] = Date(type.ValidFrom),
            };
        }

        string AddRow(string id)
        {
            if (rows.ContainsKey(id)) return id;
            var row = Result(id);
            AddModel(row.ModelId.Value);
            AddBench(row.BenchmarkTypeId);
            var item = new JsonObject
            {
                ["bench"] = row.BenchmarkTypeId,
                ["model"] = row.ModelId.Value,
                ["effort"] = EffortKey(row.ReasoningEffort),
                ["score"] = Round(row.Score, 2),
                ["cls"] = row.EvidenceClass ?? EvidenceClasses.Measured,
                ["url"] = row.SourceUrl,
                ["published"] = Date(row.PublishedAt),
                ["retrieved"] = Date(row.RetrievedAt),
            };
            if (row.Context?.ConfidenceIntervalHalfWidth is { } half)
                item["ci"] = Round(half, 1);
            if (row.SecondaryMetrics?.CostPerTaskUsd is { } cost)
            {
                item["cost"] = Round(cost, 4);
                // Creative costs are derived from published token counts at list price ([L]); others are measured.
                item["costKind"] = row.CostSource == BenchmarkCostSource.Estimated ? "derived" : "measured";
            }
            if (row.Context?.AdditionalSourceUrls is { Count: > 0 } extra)
                item["urls"] = new JsonArray(extra.Select(url => (JsonNode)url).ToArray());
            if (row.Context?.SampleNotes is { } notes && ScenesInVoting().Match(notes) is { Success: true } scenes)
                item["scenes"] = $"{scenes.Groups[1].Value} of {scenes.Groups[2].Value}";
            if (row.Context?.SampleNotes is { } votes && Votes().Match(votes) is { Success: true } count)
                item["votes"] = int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture);
            if (row.BenchmarkTypeId.StartsWith("terminal-bench", StringComparison.Ordinal))
            {
                using var raw = JsonDocument.Parse(row.EvidenceExcerpt["[M] ".Length..]);
                var metrics = raw.RootElement.GetProperty("metrics");
                item["outputTokens"] = metrics.GetProperty("output_tokens").GetInt64();
                item["cachedInputTokens"] = metrics.GetProperty("cached_input_tokens").GetInt64();
                item["totalTokens"] = metrics.GetProperty("total_tokens").GetInt64();
                item["trials"] = metrics.GetProperty("n_trials").GetInt32();
            }
            rows[id] = item;
            return id;
        }

        // Evidence entries: only current-model rows are listed for display; every entry gets a date and links.
        var evidence = new JsonObject();
        var fleet = dial.FleetCohorts.Single();
        foreach (var entry in dial.Evidence)
        {
            var listed = entry.BenchmarkResultIds.Where(id => IsCurrent(Result(id).ModelId.Value)).Select(AddRow).ToArray();
            var urls = new List<JsonObject>();
            var dates = new List<DateOnly>();
            foreach (var id in entry.BenchmarkResultIds)
            {
                var row = Result(id);
                dates.Add(row.RetrievedAt);
                AddLink(urls, row.SourceUrl, Clean((main.FindType(row.BenchmarkTypeId) ?? creative.FindType(row.BenchmarkTypeId))!.Name));
            }
            foreach (var id in entry.SubscriptionObservationIds)
            {
                var observation = subscriptions.Observations.Single(o => o.Id == id);
                if (observation.RetrievedAt is { } retrieved) dates.Add(retrieved);
                AddLink(urls, observation.SourceUrl!, "OpenAI Codex pricing");
            }
            foreach (var url in entry.SourceUrls) AddLink(urls, url, "OpenAI Codex model guide");
            if (entry.RetrievedAt is { } at) dates.Add(at);
            if (entry.FleetRowIds.Count > 0)
            {
                dates.Add(fleet.WindowEnd);
                AddLink(urls, DialDocs + "#fleet-cohort-seeded-f", "Fleet cohort record");
            }
            evidence[entry.Id] = new JsonObject
            {
                ["cls"] = entry.EvidenceClass,
                ["scope"] = entry.Scope,
                ["title"] = Clean(entry.Title),
                ["date"] = Date(dates.Max()),
                ["note"] = Clean(entry.Note),
                ["links"] = new JsonArray(urls.Select(u => (JsonNode)u).ToArray()),
                ["rows"] = Strings(listed),
            };
        }

        // Work classes, ladders, and the pick at every position (from EffortDial.Pick, not re-implemented).
        var classes = new JsonArray();
        foreach (var workClass in dial.WorkClasses)
        {
            var ladders = new JsonObject();
            foreach (var cli in new[] { DialCli.Claude, DialCli.Codex })
            {
                var ladder = workClass.Ladder(cli);
                var rungs = new JsonArray();
                for (var index = 0; index < ladder.Rungs.Count; index++)
                {
                    var rung = ladder.Rungs[index];
                    AddModel(rung.ModelId);
                    var next = ladder.Rungs.Skip(index + 1).FirstOrDefault(r => r.FromPosition is not null);
                    var cost = dial.CostOf(rung.Id);
                    var item = new JsonObject
                    {
                        ["id"] = rung.Id,
                        ["model"] = rung.ModelId,
                        ["effort"] = EffortKey(rung.Effort),
                        ["from"] = rung.FromPosition,
                        ["to"] = rung.FromPosition is null ? null : (next?.FromPosition - 1 ?? dial.Positions.Maximum),
                        ["escalationOnly"] = rung.EscalationOnly,
                        ["selectable"] = dial.IsSelectable(rung),
                        ["confidence"] = rung.Confidence,
                        ["evidence"] = Strings(rung.EvidenceIds),
                        ["cost"] = Cost(cost),
                    };
                    if (rung.Restriction is { } restriction) item["restriction"] = Clean(restriction);
                    if (rung.Note is { } note) item["note"] = Clean(note);
                    if (rung.Dimensions is { } dimensions)
                    {
                        item["dimensions"] = new JsonObject
                        {
                            ["originality"] = Dimension(dimensions.Originality),
                            ["design"] = Dimension(dimensions.Design),
                            ["gameFeel"] = Dimension(dimensions.GameFeel),
                            ["technicalExecution"] = Dimension(dimensions.TechnicalExecution),
                        };
                    }
                    rungs.Add(item);
                }
                var segments = new JsonArray();
                DialPick? previous = null;
                var start = dial.Positions.Minimum;
                for (var position = dial.Positions.Minimum; position <= dial.Positions.Maximum + 1; position++)
                {
                    var pick = position <= dial.Positions.Maximum ? dial.Pick(position, workClass.Id, cli) : null;
                    if (previous is not null && (pick is null || pick.Rung.Id != previous.Rung.Id))
                    {
                        segments.Add(new JsonObject
                        {
                            ["from"] = start,
                            ["to"] = position - 1,
                            ["rung"] = previous.Rung.Id,
                            ["skipped"] = Strings(previous.SkippedRestrictedRungIds),
                        });
                        start = position;
                    }
                    previous = pick;
                }
                ladders[EffortDialKey(cli)] = new JsonObject
                {
                    ["rungs"] = rungs,
                    ["picks"] = segments,
                    ["candidates"] = new JsonArray(ladder.Candidates.Select(candidate =>
                    {
                        AddModel(candidate.ModelId);
                        return (JsonNode)new JsonObject
                        {
                            ["model"] = candidate.ModelId,
                            ["effort"] = EffortKey(candidate.Effort),
                            ["evidence"] = Strings(candidate.EvidenceIds),
                            ["reason"] = Clean(candidate.Reason),
                        };
                    }).ToArray()),
                };
            }
            classes.Add(new JsonObject
            {
                ["id"] = workClass.Id,
                ["letter"] = workClass.Letter,
                ["label"] = Clean(workClass.Label),
                ["description"] = Clean(workClass.Description),
                ["basis"] = workClass.EvidenceBasis,
                ["confidence"] = workClass.Confidence,
                ["floors"] = new JsonArray(workClass.Floors.Select(floor => (JsonNode)new JsonObject
                {
                    ["cli"] = floor.Cli,
                    ["condition"] = floor.Condition,
                    ["rung"] = floor.RungId,
                    ["reason"] = Clean(floor.Reason),
                }).ToArray()),
                ["gaps"] = Strings(workClass.EvidenceGaps.Select(Clean)),
                ["ladders"] = ladders,
            });
        }

        var conclusions = new JsonArray(dial.Conclusions.Select(conclusion => (JsonNode)new JsonObject
        {
            ["id"] = conclusion.Id,
            ["statement"] = Clean(conclusion.Statement),
            ["appliesTo"] = Strings(conclusion.AppliesTo),
            ["provisionalFor"] = Strings(conclusion.ProvisionalFor),
            ["doesNotApplyTo"] = Strings(conclusion.DoesNotApplyTo),
            ["evidence"] = Strings(conclusion.EvidenceIds),
        }).ToArray());

        // Creative rubric and the boards behind it, with every current-model row.
        var creativeBoards = new JsonArray();
        foreach (var typeId in CreativeBoards)
        {
            AddBench(typeId);
            var rubric = dial.CreativeRubric.Benchmarks.Single(b => b.BenchmarkTypeId == typeId);
            var boardRows = creative.ResultsFor(typeId).Where(r => IsCurrent(r.ModelId.Value))
                .OrderByDescending(r => r.Score).Select(r => AddRow(r.Id)).ToArray();
            var board = new JsonObject
            {
                ["bench"] = typeId,
                ["method"] = rubric.Method,
                ["judged"] = Clean(rubric.Judged),
                ["coverage"] = Clean(rubric.Coverage),
                ["dimensions"] = new JsonObject
                {
                    ["originality"] = rubric.Dimensions.Originality,
                    ["design"] = rubric.Dimensions.Design,
                    ["gameFeel"] = rubric.Dimensions.GameFeel,
                    ["technicalExecution"] = rubric.Dimensions.TechnicalExecution,
                },
                ["excludesBrokenBuilds"] = rubric.ExcludesBrokenBuildsBeforeVote,
                ["publishesNonRunningShare"] = rubric.PublishesNonRunningShare,
                ["publishesCost"] = rubric.PublishesCost,
                ["notes"] = Clean(rubric.Notes),
                ["gaps"] = Strings(rubric.Gaps.Select(Clean)),
                ["rows"] = Strings(boardRows),
            };
            if (typeId.StartsWith("aa-3d", StringComparison.Ordinal)) board["explorer"] = AaExplorer;
            creativeBoards.Add(board);
        }

        // Effort ladders: every row of the listed models on the three benchmarks.
        var ladderBenches = new JsonArray();
        foreach (var (id, typeId, per) in LadderBenchmarks)
        {
            AddBench(typeId);
            var benchRows = main.ResultsFor(typeId).Where(r => LadderModels.Contains(r.ModelId.Value))
                .OrderBy(r => LadderModels.IndexOf(r.ModelId.Value)).ThenBy(r => r.ReasoningEffort)
                .Select(r => AddRow(r.Id)).ToArray();
            ladderBenches.Add(new JsonObject { ["id"] = id, ["bench"] = typeId, ["per"] = per, ["rows"] = Strings(benchRows) });
        }
        foreach (var model in LadderModels) AddModel(model);

        // Fleet cohort (class F), anonymised: no card keys or project names.
        var cache = fleet.CacheReadObservations.Single(o => o.Id == FleetCacheObservation);
        foreach (var row in fleet.Rows) AddModel(row.ModelId);
        var fleetNode = new JsonObject
        {
            ["cls"] = fleet.EvidenceClass,
            ["windowStart"] = Date(fleet.WindowStart),
            ["windowEnd"] = Date(fleet.WindowEnd),
            ["days"] = fleet.WindowEnd.DayNumber - fleet.WindowStart.DayNumber,
            ["population"] = "Coding tasks whose cost is pure: one model and reasoning level carries at least 95% of the task's list-price cost.",
            ["priceBasis"] = Clean(fleet.PriceBasis),
            ["retryTax"] = fleet.RetryTaxShareOfSpend,
            ["totalListUsd"] = fleet.TotalListSpendUsd,
            ["codingShare"] = fleet.CodingShareOfSpend,
            ["caveats"] = Strings(fleet.Caveats.Select(Clean)),
            ["record"] = DialDocs + "#fleet-cohort-seeded-f",
            ["rows"] = new JsonArray(fleet.Rows.Select(row => (JsonNode)new JsonObject
            {
                ["id"] = row.Id,
                ["model"] = row.ModelId,
                ["effort"] = EffortKey(row.Effort),
                ["cards"] = row.Cards,
                ["accepted"] = row.Accepted,
                ["firstReviewPassed"] = row.FirstReviewPassed,
                ["firstReviewed"] = row.FirstReviewed,
                ["usdPerAccepted"] = row.MedianListUsdPerAcceptedCard,
                ["tokensPerAccepted"] = row.MedianTokensPerAcceptedCard,
            }).ToArray()),
            ["cacheReads"] = new JsonArray(fleet.CacheReadObservations.Select(o => (JsonNode)new JsonObject
            {
                ["model"] = o.ModelId,
                ["effort"] = EffortKey(o.Effort),
                ["tokens"] = o.CachedReadTokens,
                ["share"] = o.CacheReadShareOfListCost,
                ["note"] = Clean(o.Note),
            }).ToArray()),
        };

        // Cost drivers: the cache lever, derived from the fleet share and the two price cards.
        var from = models["gpt-6-sol"];
        AddModel("gpt-6.1-sol");
        var to = models["gpt-6.1-sol"];
        var share = cache.CacheReadShareOfListCost ?? throw new InvalidOperationException("Fleet cache share missing");
        var cacheRatio = (decimal)to["cacheRead"]! / (decimal)from["cacheRead"]!;
        var inputRatio = (decimal)to["input"]! / (decimal)from["input"]!;
        var factor = share * cacheRatio + (1 - share) * inputRatio;
        var anchor = fleet.Rows.Single(r => r.ModelId == cache.ModelId && r.Effort == cache.Effort);
        var repriced = dial.Pick(dial.Positions.Maximum, "feature-logic", DialCli.Codex).Cost;
        var costDrivers = new JsonObject
        {
            ["from"] = "gpt-6-sol",
            ["to"] = "gpt-6.1-sol",
            ["cacheShare"] = share,
            ["cachedTokens"] = cache.CachedReadTokens,
            ["effort"] = EffortKey(cache.Effort),
            ["factor"] = Round(factor, 4),
            ["anchorUsd"] = anchor.MedianListUsdPerAcceptedCard,
            ["repricedUsd"] = repriced.ListUsdLow,
            ["aa3dShares"] = new JsonArray(creative.ResultsFor("aa-3d-v1.0-2026-11-27")
                .Where(r => IsCurrent(r.ModelId.Value) && r.CostDerivation is not null && CacheShare().IsMatch(r.CostDerivation))
                .Select(r => (JsonNode)new JsonObject
                {
                    ["row"] = AddRow(r.Id),
                    ["share"] = decimal.Parse(CacheShare().Match(r.CostDerivation!).Groups[1].Value, CultureInfo.InvariantCulture) / 100m,
                }).ToArray()),
            ["credits"] = new JsonArray(subscriptions.Observations
                .Where(o => o.Kind == "credit-rate-card" && o.ModelId is not null && (IsCurrent(o.ModelId) || o.ModelId == "gpt-6-sol"))
                .Select(o =>
                {
                    AddModel(o.ModelId!);
                    return (JsonNode)new JsonObject
                    {
                        ["model"] = o.ModelId,
                        ["input"] = o.InputCreditsPerMTok,
                        ["cacheRead"] = o.CacheReadCreditsPerMTok,
                        ["output"] = o.OutputCreditsPerMTok,
                        ["url"] = o.SourceUrl,
                        ["retrieved"] = o.RetrievedAt is { } d ? Date(d) : null,
                        ["cls"] = o.EvidenceClass,
                    };
                }).ToArray()),
        };

        var subscriptionNode = new JsonObject
        {
            ["statements"] = new JsonArray(subscriptions.Observations.Where(o => o.Kind == "vendor-statement")
                .Select(o => (JsonNode)new JsonObject
                {
                    ["lab"] = o.Provider,
                    ["cli"] = o.CliId,
                    ["quote"] = o.Quote,
                    ["meaning"] = Clean(o.Note.Replace("[V] ", "", StringComparison.Ordinal)),
                    ["url"] = o.SourceUrl,
                    ["retrieved"] = o.RetrievedAt is { } d ? Date(d) : null,
                    ["cls"] = o.EvidenceClass,
                }).ToArray()),
            ["messages"] = new JsonArray(subscriptions.Observations
                .Where(o => o.Kind == "message-range" && o.ModelId is not null && IsCurrent(o.ModelId))
                .Select(o =>
                {
                    AddModel(o.ModelId!);
                    return (JsonNode)new JsonObject
                    {
                        ["model"] = o.ModelId,
                        ["plan"] = o.PlanId,
                        ["window"] = o.Window,
                        ["min"] = o.MinimumMessages,
                        ["max"] = o.MaximumMessages,
                        ["url"] = o.SourceUrl,
                        ["retrieved"] = o.RetrievedAt is { } d ? Date(d) : null,
                        ["cls"] = o.EvidenceClass,
                    };
                }).ToArray()),
        };

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["asOf"] = Date(dial.AsOfDate),
            ["decidedOn"] = Date(dial.Decision.DecidedOn),
            ["positions"] = new JsonObject
            {
                ["min"] = dial.Positions.Minimum,
                ["max"] = dial.Positions.Maximum,
                ["default"] = dial.Positions.Default,
            },
            ["docs"] = new JsonObject { ["dial"] = DialDocs, ["creative"] = CreativeDocs },
            ["evidenceClasses"] = new JsonArray(dial.EvidenceClassDefinitions.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["label"] = c.Label,
                ["description"] = Clean(c.Description),
            }).ToArray()),
            ["rubric"] = new JsonArray(dial.CreativeRubric.Dimensions.Select(d => (JsonNode)new JsonObject
            {
                ["id"] = d.Id,
                ["label"] = d.Label,
                ["question"] = Clean(d.Question),
                ["example"] = Clean(d.GameExample),
            }).ToArray()),
            ["classes"] = classes,
            ["conclusions"] = conclusions,
            ["creativeBoards"] = creativeBoards,
            ["ladders"] = ladderBenches,
            ["fleet"] = fleetNode,
            ["costDrivers"] = costDrivers,
            ["subscriptions"] = subscriptionNode,
            ["evidence"] = evidence,
            ["benchmarks"] = ToObject(benches),
            ["rows"] = ToObject(rows),
            ["models"] = ToObject(models),
        };
    }

    private static JsonObject Cost(DialRungCost cost) => new()
    {
        ["status"] = cost.Status.ToString().ToLowerInvariant(),
        ["unit"] = cost.Unit switch
        {
            DialCostUnit.AcceptedCard => "acceptedCard",
            DialCostUnit.BenchmarkTask => "benchmarkTask",
            _ => null,
        },
        ["low"] = cost.ListUsdLow,
        ["high"] = cost.ListUsdHigh,
        ["creditsLow"] = cost.CodexCreditsLow,
        ["creditsHigh"] = cost.CodexCreditsHigh,
        ["basis"] = cost.QuotaBasis switch
        {
            DialQuotaBasis.CodexCreditRate => "codexCredits",
            DialQuotaBasis.CodexCreditsUnavailable => "codexNoCredits",
            DialQuotaBasis.ClaudeListPriceProxy => "claudeListPriceProxy",
            DialQuotaBasis.ClaudeMeasuredWeight => "claudeMeasured",
            _ => throw new InvalidOperationException($"Unknown quota basis {cost.QuotaBasis}"),
        },
        ["derivation"] = Strings(cost.Derivation.Select(Clean)),
        ["unknownReason"] = cost.UnknownReason is null ? null : Clean(cost.UnknownReason),
    };

    private static JsonObject Dimension(DialRungDimension dimension) => new()
    {
        ["status"] = dimension.Status,
        ["text"] = Clean(dimension.Text),
        ["evidence"] = Strings(dimension.EvidenceIds),
    };

    private static void AddLink(List<JsonObject> links, string url, string label)
    {
        if (links.Any(link => (string?)link["url"] == url)) return;
        links.Add(new JsonObject { ["label"] = label, ["url"] = url });
    }

    /// <summary>
    /// Public page text: no card keys and never the words this site reserves for the Studio's own decisions.
    /// </summary>
    public static string Clean(string text)
    {
        var cleaned = text
            .Replace(" (TE-82)", "", StringComparison.Ordinal)
            .Replace("by TE-83", "by us", StringComparison.Ordinal)
            .Replace("(TE-83 count", "(our count", StringComparison.Ordinal)
            .Replace("TE-83 counted", "We counted", StringComparison.Ordinal)
            .Replace("restricted in routing", "not yet cleared for our fleet", StringComparison.Ordinal)
            .Replace("Restricted in routing", "Not yet cleared for our fleet", StringComparison.Ordinal)
            .Replace("routing-restriction", "fleet-clearance", StringComparison.Ordinal)
            .Replace("Agent Studio 28-day", "28-day", StringComparison.Ordinal)
            .Replace("on Agent Studio runs", "on our fleet's runs", StringComparison.Ordinal)
            .Replace("Agent Studio", "our fleet", StringComparison.Ordinal)
            .Replace("per-card", "per-task", StringComparison.Ordinal);
        // The page speaks of tasks; a "rate card" stays a rate card.
        cleaned = CardWord().Replace(cleaned, match => (char.IsUpper(match.Value[0]) ? "Task" : "task") + match.Groups[1].Value);
        if (CardKey().IsMatch(cleaned) || BannedWord().IsMatch(cleaned))
            throw new InvalidOperationException($"Page text needs editing before publication: {cleaned}");
        return cleaned;
    }

    private static string EffortKey(EffortLevel effort) => effort.ToString().ToLowerInvariant();

    private static string EffortDialKey(DialCli cli) => cli == DialCli.Claude ? "claude" : "codex";

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal Round(decimal value, int digits) => decimal.Round(value, digits, MidpointRounding.AwayFromZero);

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)v).ToArray());

    private static JsonObject ToObject(SortedDictionary<string, JsonObject> items)
    {
        var result = new JsonObject();
        foreach (var (key, value) in items) result[key] = value;
        return result;
    }

    private static int IndexOf(this IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return -1;
    }

    [GeneratedRegex(@"(?<!rate )\b[Cc]ard('s|s)?\b")]
    private static partial Regex CardWord();

    [GeneratedRegex(@"\bTE-\d+\b")]
    private static partial Regex CardKey();

    [GeneratedRegex(@"\brout(e|es|ed|ing)\b", RegexOptions.IgnoreCase)]
    public static partial Regex BannedWord();

    [GeneratedRegex(@"(\d+) of (\d+) AA-3D Explorer tasks")]
    private static partial Regex ScenesInVoting();

    [GeneratedRegex(@"^(\d+) votes")]
    private static partial Regex Votes();

    [GeneratedRegex(@"Cache-read share of the derived cost: ([\d.]+)%")]
    private static partial Regex CacheShare();
}
