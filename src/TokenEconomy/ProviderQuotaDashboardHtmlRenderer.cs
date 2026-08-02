using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

/// <summary>Renders an accessible, dependency-free provider availability snapshot.</summary>
public static class ProviderQuotaDashboardHtmlRenderer
{
    /// <summary>Renders one card per provider/CLI/quota-window identity without collapsing limits.</summary>
    public static string Render(IEnumerable<ProviderQuotaDashboardRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var materialized = rows.ToArray();
        var decisionAt = materialized.Select(row => row.DecisionAtUtc).Where(value => value != default).Distinct().ToArray();
        var html = new StringBuilder("<section class=\"provider-quota-dashboard\" aria-label=\"Provider availability snapshot\"><h1>Provider availability snapshot</h1>");
        if (decisionAt.Length == 1)
            html.Append("<p class=\"decision-time\">Decision time: <time datetime=\"").Append(Iso(decisionAt[0])).Append("\">").Append(Date(decisionAt[0])).Append("</time></p>");
        html.Append("<p class=\"snapshot-caveat\">Availability evidence only; this view does not select a model or lower a routing-policy correctness floor.</p><div class=\"quota-grid\">");
        foreach (var row in materialized)
        {
            var state = row.State.ToString().ToLowerInvariant();
            var cli = string.IsNullOrWhiteSpace(row.Cli) ? "CLI unknown" : row.Cli;
            html.Append("<article class=\"quota-card quota-").Append(state).Append("\"><header><div><h2>").Append(Escape(row.Provider))
                .Append("</h2><p class=\"cli\">").Append(Escape(cli)).Append("</p></div><span class=\"state ").Append(state).Append("\">").Append(state).Append("</span></header>")
                .Append("<p class=\"window-identity\"><strong>").Append(Escape(row.WindowLabel)).Append("</strong> <code>").Append(Escape(row.WindowId)).Append("</code></p>")
                .Append("<dl class=\"availability\"><dt>Provider / CLI</dt><dd>").Append(Escape(row.Availability.ToString())).Append("</dd><dt>Availability freshness</dt><dd>")
                .Append(Escape(row.AvailabilityFreshness.ToString())).Append(row.AvailabilityObservedAtUtc is { } availabilityAt ? " · " + Date(availabilityAt) : " · no observation").Append("</dd></dl>");

            var observation = row.QuotaObservation;
            var meterValue = observation?.UsedPercent ?? row.QuotaMarkPercent;
            var meterWidth = Math.Clamp(meterValue, 0m, 100m);
            html.Append("<div class=\"quota-meter\" role=\"progressbar\" aria-label=\"").Append(Escape(row.Provider)).Append(" ").Append(Escape(row.WindowLabel)).Append(" configured mark used\" aria-valuenow=\"")
                .Append(Number(meterValue)).Append("\" aria-valuemin=\"0\" aria-valuemax=\"100\"><span style=\"width:").Append(Number(meterWidth)).Append("%\"></span></div><dl class=\"quota-observation\">")
                .Append("<dt>Quota usage</dt><dd>").Append(observation?.UsedTokens is { } used ? $"{Number(used)} / {Number(observation.ConfiguredMarkTokens)} tokens ({Number(observation.UsedPercent ?? 0)}%)" : "Unknown — no quota observation").Append("</dd>")
                .Append("<dt>Headroom</dt><dd>").Append(observation?.HeadroomTokens is { } headroom ? Number(headroom) + " tokens" : "Unknown").Append("</dd>")
                .Append("<dt>Usage source</dt><dd>").Append(Escape(Source(observation?.Source))).Append("</dd>")
                .Append("<dt>Quota freshness</dt><dd>").Append(Escape(observation?.Freshness.ToString() ?? AvailabilityFreshness.Missing.ToString())).Append(observation?.ObservedAtUtc is { } quotaAt ? " · " + Date(quotaAt) : " · no observation").Append("</dd>")
                .Append("<dt>Reset</dt><dd>").Append(observation?.ResetsAtUtc is { } reset ? Date(reset) : "Unknown").Append("</dd></dl>");

            var projection = row.Projection;
            html.Append("<section class=\"projection\"><h3>Inferred projection</h3><dl><dt>Recent rate</dt><dd>").Append(Number(projection?.TokensPerHour ?? row.TokensPerHour)).Append(" tokens/hour</dd>")
                .Append("<dt>Projected exhaustion</dt><dd>").Append((projection?.ProjectedExhaustionAtUtc ?? row.ProjectedMarkAtUtc) is { } projected ? Date(projected) : "Unavailable — no usable recent rate or quota observation").Append("</dd></dl></section>");

            html.Append("<section class=\"cost-status\"><h3>Cost status at decision time</h3>");
            if (row.CostSnapshot is { } cost)
            {
                html.Append("<p><strong>").Append(Escape(CostLabel(cost.Status))).Append("</strong> · ").Append(Date(cost.PricedAtUtc)).Append("</p><ul>");
                foreach (var model in cost.Models)
                    html.Append("<li><code>").Append(Escape(model.ModelId)).Append("</code>: ").Append(Escape(ModelCostLabel(model))).Append("</li>");
                if (cost.Models.Count == 0) html.Append("<li>No model identity available; cost is unknown, not free.</li>");
                html.Append("</ul>");
            }
            else html.Append("<p><strong>Unknown</strong> — no decision-time price evidence; cost is not free.</p>");
            html.Append("</section><section class=\"warnings\"><h3>Warning state</h3>");
            if (row.WarningReasons.Count == 0) html.Append("<p>None</p>");
            else
            {
                html.Append("<ul>");
                foreach (var reason in row.WarningReasons) html.Append("<li>").Append(Escape(reason)).Append("</li>");
                html.Append("</ul>");
            }
            html.Append("</section><section class=\"tier-shares\"><h3>Imported-run tier share (context only)</h3><ul>");
            foreach (var share in row.ModelShares)
                html.Append("<li><span>").Append(Escape(share.Tier)).Append("</span><span class=\"tier-bar\"><i style=\"width:").Append(Number(Math.Clamp(share.Percent, 0m, 100m))).Append("%\"></i></span><b>").Append(Number(share.Percent)).Append("%</b></li>");
            if (!row.ModelShares.Any()) html.Append("<li class=\"no-tier-consumption\">No eligible imported-run consumption</li>");
            html.Append("</ul></section></article>");
        }
        return html.Append("</div></section>").ToString();
    }

    private static string Source(QuotaUsageSource? source) => source switch
    {
        QuotaUsageSource.ObservedProviderMeter => "Observed provider meter",
        QuotaUsageSource.InferredFromImportedRuns => "Inferred from imported runs",
        _ => "Missing",
    };

    private static string CostLabel(ProviderCostStatus status) => status switch
    {
        ProviderCostStatus.Priced => "Priced from dated catalog",
        ProviderCostStatus.PartiallyPriced => "Partially priced — unresolved models remain unknown",
        ProviderCostStatus.Unconfirmed => "Unconfirmed list price",
        ProviderCostStatus.Unpriced => "Unpriced — not free",
        _ => "Unknown — not free",
    };

    private static string ModelCostLabel(ProviderModelCostStatus model)
        => model.Status == PriceStatus.Resolved
            ? model.Unconfirmed ? $"unconfirmed list price ({model.Currency})" : $"priced ({model.Currency})"
            : model.Status switch
            {
                PriceStatus.NoPriceForDate => "no price for decision time — not free",
                PriceStatus.UnknownModel => "unknown model cost — not free",
                _ => "usage/cost unavailable — not free",
            };

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
    private static string Iso(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
