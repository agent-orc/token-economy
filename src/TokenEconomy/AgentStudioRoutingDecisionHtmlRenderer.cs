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
