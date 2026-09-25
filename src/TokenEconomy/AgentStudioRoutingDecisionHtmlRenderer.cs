using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

/// <summary>Renders one persisted routing/admission decision for an Agent Studio operator surface.</summary>
public static class AgentStudioRoutingDecisionHtmlRenderer
{
    /// <summary>
    /// Renders only persisted policy fields. It never derives or changes a recommendation, model,
    /// or thinking level outside the canonical routing decision.
    /// </summary>
    public static string Render(AgentStudioRoutingDecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var html = new StringBuilder("<article class=\"routing-decision-card\" aria-label=\"Model routing decision\"><header><h2>Model routing decision</h2><span class=\"routing-disposition\">")
            .Append(Escape(decision.Disposition?.ToString() ?? "Legacy decision"))
            .Append("</span></header><dl class=\"routing-decision-facts\">");
        Fact(html, "Recommended route", Route(decision.RecommendedRouteId, decision.RecommendedModel, decision.RecommendedThinkingLevel));
        Fact(html, "Selected route", Route(decision.SelectedRouteId, decision.SelectedModel, decision.SelectedThinkingLevel, "No route selected"));
        Fact(html, "Score", Score(decision));
        Fact(html, "Hard floor", HardFloor(decision));
        Fact(html, "Selection source", decision.SelectionSource ?? "Unknown");
        Fact(html, "Policy version", decision.PolicyVersion ?? "Unknown");
        Fact(html, "Recommended provisional", YesNoUnknown(decision.RecommendedRouteProvisional));
        Fact(html, "Selected provisional", YesNoUnknown(decision.SelectedRouteProvisional));
        Fact(html, "Quota fallback", decision.QuotaFallbackApplied == true
            ? decision.QuotaFallbackReason ?? "Applied"
            : decision.QuotaFallbackApplied == false ? "Not applied" : "Unknown");
        Fact(html, "Configured card route", Route(null, decision.ConfiguredModel, decision.ConfiguredThinkingLevel, "Not recorded"));
        Fact(html, "Pin warning", decision.OperatorPinWarning ?? (decision.OperatorPinBelowPolicy == false ? "None" : "Not applicable"));
        Fact(html, "Wait or override reason", decision.WaitOrOverrideReason ?? "Not waiting");
        Fact(html, "Quota snapshot", decision.QuotaSnapshotId ?? "Unknown");
        Fact(html, "Quota decision time", decision.QuotaSnapshotDecisionAtUtc is { } quotaAt
            ? quotaAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "Unknown");
        return html.Append("</dl></article>").ToString();
    }

    /// <summary>Compose the persisted route with a freshly evaluated, advisory economics query.</summary>
    public static string Render(AgentStudioRoutingDecisionRecord decision, CardEconomicsQuery query,
        IEnumerable<AgentStudioRunRecord> records)
        => Render(decision) + RenderEconomics(new CardEconomics().Decide(query, records));

    /// <summary>Compose a routing decision with the dated export evidence.</summary>
    public static string Render(AgentStudioRoutingDecisionRecord decision, CardEconomicsQuery query, AgentStudioCohort cohort)
        => Render(decision) + RenderEconomics(new CardEconomics().Decide(query, cohort));

    /// <summary>Render unknowns and evidence coverage alongside both currencies without changing admission.</summary>
    public static string RenderEconomics(CardEconomicsDecision decision)
    {
        var html = new StringBuilder("<section class=\"routing-decision-card\" aria-label=\"Card economics\"><h2>Cost per completed card</h2><p>")
            .Append(Escape(decision.Recommendation)).Append("</p><p>USD: estimated - list prices. Duration includes observed attempt time; queue time is unknown.</p>")
            .Append("<table><thead><tr><th>Rank</th><th>Model / level</th><th>USD</th><th>Weekly quota %</th><th>Seconds</th><th>Completed / cards; rounds</th><th>Favorable / known reviews</th><th>Confidence</th><th>Availability</th><th>Eligibility</th></tr></thead><tbody>");
        foreach (var row in decision.Rows)
        {
            html.Append("<tr>");
            foreach (var cell in new[]
            {
                row.Rank.ToString(CultureInfo.InvariantCulture),
                $"{row.Candidate.Model} / {row.Candidate.ThinkingLevel}; selectable={row.Selectable}; provisional={row.Provisional}",
                Number(row.ExpectedUsdPerCompletedCard) + (row.UnconfirmedPrices ? " (unconfirmed)" : ""),
                Number(row.ExpectedWeeklyQuotaPercentPerCompletedCard), Number(row.ExpectedDurationSeconds),
                $"{row.CompletedCards} / {row.Cards}; {row.Runs} rounds; {row.ExcludedCards} cards excluded",
                $"{row.FavorableReviews} / {row.KnownReviews}", row.Confidence,
                row.Availability + $"; telemetry {row.AvailabilityObservedRuns}/{row.AvailabilityTotalRuns} runs; " + string.Join("; ", row.Errors.Select(e => $"{e.ErrorClass}: {e.Count}/{e.ObservedRuns} ({e.Rate:P1}), last {e.LastSeenUtc:O}; {e.Detail}")),
                row.EligibilityReason + " " + string.Join("; ", row.OrganisationEvents.Select(e => $"{e.EventCount} refusal events; last seen unknown; {e.Detail}")),
            }) html.Append("<td>").Append(Escape(cell)).Append("</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
        if (decision.CohortEvidence is { } evidence)
        {
            html.Append("<h3>Local export: model observations, reasoning levels unknown</h3><p>")
                .Append(Escape(evidence.Source)).Append("</p><p>Artifact: ").Append(Escape(evidence.ArtifactReference))
                .Append("; SHA-256: ").Append(Escape(evidence.Sha256)).Append("</p>");
            foreach (var caveat in evidence.Caveats) html.Append("<p>").Append(Escape(caveat)).Append("</p>");
            void Models(string label, IReadOnlyList<CohortModelSummary> models)
            {
                html.Append("<h4>").Append(Escape(label)).Append("</h4><table><thead><tr><th>Model</th><th>Runs</th><th>Total run USD</th><th>USD / run</th><th>Completed cards</th><th>Open cards</th><th>USD / completed history</th><th>Weekly % / run</th><th>Weekly % / completed card</th><th>Confidence</th></tr></thead><tbody>");
                foreach (var model in models)
                {
                    html.Append("<tr>");
                    foreach (var cell in new[] { model.Model, model.RecordedRuns.ToString(CultureInfo.InvariantCulture),
                        Number(model.TotalRunUsd), Number(model.MeanUsdPerRun), model.CompletedCards.ToString(CultureInfo.InvariantCulture),
                        model.OpenCards.ToString(CultureInfo.InvariantCulture), Number(model.UsdPerCompletedCard),
                        Number(model.WeeklyQuotaPercentPerRun), Number(model.WeeklyQuotaPercentPerCompletedCard), model.Confidence })
                        html.Append("<td>").Append(Escape(cell)).Append("</td>");
                    html.Append("</tr>");
                }
                html.Append("</tbody></table>");
            }
            Models("Runs in query window; only complete histories wholly in window", evidence.WindowModels);
            Models("All exported histories (including earlier Opus runs)", evidence.FullHistoryModels);
            html.Append("<h4>Account quota changes (percentage points; per-card attribution unknown)</h4><ul>");
            foreach (var interval in evidence.QuotaIntervals)
                html.Append("<li>").Append(Escape($"{interval.From:O} to {interval.Through:O}: Codex {Number(interval.CodexPercentagePoints)}; Claude {Number(interval.ClaudePercentagePoints)}")).Append("</li>");
            html.Append("</ul><h4>Card-level reviews (no model or attempt attribution)</h4><ul>");
            foreach (var card in evidence.Cards)
                html.Append("<li>").Append(Escape($"{card.TaskKey}: {card.Lane}; {string.Join("; ", card.Reviews)}")).Append("</li>");
            html.Append("</ul>");
        }
        return html.Append("</section>").ToString();
    }

    private static string Number(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "Unknown";

    private static void Fact(StringBuilder html, string label, string value)
        => html.Append("<dt>").Append(Escape(label)).Append("</dt><dd>").Append(Escape(value)).Append("</dd>");

    private static string Route(string? routeId, string? model, string? thinking, string fallback = "Unknown")
    {
        if (string.IsNullOrWhiteSpace(routeId) && string.IsNullOrWhiteSpace(model)) return fallback;
        if (string.IsNullOrWhiteSpace(routeId))
            return string.IsNullOrWhiteSpace(thinking) ? model! : $"{model} / {thinking}";
        var identity = routeId;
        return string.IsNullOrWhiteSpace(thinking) ? identity : $"{identity} — {model ?? identity} / {thinking}";
    }

    private static string Score(AgentStudioRoutingDecisionRecord decision)
        => decision.Score is null ? "Unknown"
            : decision.UpfrontScore is { } upfront && upfront != decision.Score
                ? $"{upfront}/100 intake; {decision.Score}/100 effective"
                : $"{decision.Score}/100";

    private static string HardFloor(AgentStudioRoutingDecisionRecord decision)
    {
        var route = Route(decision.HardFloorRouteId, decision.HardFloorModel, decision.HardFloorThinkingLevel);
        if (decision.AppliedHardFloorIds.Count == 0)
            return decision.IsHardFloor == false ? $"{route}; no applied hard floor" : route;
        return $"{route}; {string.Join(", ", decision.AppliedHardFloorIds)}";
    }

    private static string YesNoUnknown(bool? value) => value is null ? "Unknown" : value.Value ? "Yes" : "No";
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
