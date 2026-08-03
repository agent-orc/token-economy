using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

/// <summary>Renders an accessible, dependency-free provider quota dashboard view.</summary>
public static class ProviderQuotaDashboardHtmlRenderer
{
    /// <summary>Renders a routing-grade availability snapshot without making a routing decision.</summary>
    public static string RenderSnapshot(ProviderAvailabilitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var html = new StringBuilder("<section class=\"provider-availability-snapshot\" aria-label=\"Provider availability snapshot\"><header class=\"snapshot-header\"><h1>Provider availability snapshot</h1><p>Decision time <time datetime=\"")
            .Append(snapshot.DecisionAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append("\">")
            .Append(snapshot.DecisionAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))
            .Append("</time></p><p class=\"snapshot-scope\">Availability evidence only; no model selection is performed.</p></header><div class=\"quota-grid\">");

        foreach (var row in snapshot.Providers)
        {
            var state = Css(row.WarningState);
            html.Append("<article class=\"quota-card quota-").Append(state).Append("\"><header><div><h2>")
                .Append(Escape(row.Provider)).Append("</h2><p class=\"cli-type\">CLI ").Append(Escape(row.CliType))
                .Append("</p></div><span class=\"state ").Append(state).Append("\">").Append(Escape(Label(row.WarningState)))
                .Append("</span></header><dl class=\"provider-facts\"><dt>CLI availability</dt><dd>")
                .Append(Escape(Label(row.Availability))).Append("</dd><dt>Freshness</dt><dd>")
                .Append(Escape(Label(row.Freshness))).Append("</dd><dt>Availability observed</dt><dd>")
                .Append(row.AvailabilityObservedAtUtc is { } availabilityAt ? Time(availabilityAt) : "Missing")
                .Append("</dd><dt>Cost at decision time</dt><dd class=\"cost-status cost-")
                .Append(Css(row.Cost.Status)).Append("\">").Append(Escape(CostLabel(row.Cost.Status))).Append("</dd></dl>");
            if (!string.IsNullOrWhiteSpace(row.AvailabilityDetail))
                html.Append("<p class=\"availability-detail\">").Append(Escape(row.AvailabilityDetail)).Append("</p>");

            html.Append("<p class=\"rate\">").Append(Number(row.TokensPerHour)).Append(" <span>tokens/hour</span></p><p class=\"trailing-window\">Inferred rate from measured runs in the trailing ")
                .Append(Duration(snapshot.TrailingWindow)).Append("</p><section class=\"quota-windows\"><h3>Quota windows</h3>");
            foreach (var window in row.QuotaWindows)
            {
                var windowState = Css(window.WarningState);
                html.Append("<article class=\"quota-window quota-").Append(windowState).Append("\"><header><h4>")
                    .Append(Escape(window.WindowId)).Append("</h4><span class=\"state ").Append(windowState).Append("\">")
                    .Append(Escape(Label(window.WarningState))).Append("</span></header><dl><dt>Freshness</dt><dd>")
                    .Append(Escape(Label(window.Freshness))).Append("</dd><dt>Reset</dt><dd>")
                    .Append(window.ResetsAtUtc is { } reset ? Time(reset) : "Unknown").Append("</dd>");
                if (window.Usage is { } usage)
                {
                    var meterPercent = Math.Clamp(usage.UsedPercent, 0m, 100m);
                    html.Append("<dt>Observed usage</dt><dd>").Append(Number(usage.UsedTokens)).Append(" / ")
                        .Append(Number(usage.LimitTokens)).Append(" tokens (").Append(Number(usage.UsedPercent))
                        .Append("%)</dd><dt>Observed headroom</dt><dd>").Append(Number(usage.HeadroomTokens))
                        .Append(" tokens</dd></dl><div class=\"quota-meter\" role=\"progressbar\" aria-label=\"")
                        .Append(Escape(window.WindowId)).Append(" observed quota used\" aria-valuenow=\"")
                        .Append(Number(meterPercent)).Append("\" aria-valuemin=\"0\" aria-valuemax=\"100\"><span style=\"width:")
                        .Append(Number(meterPercent)).Append("%\"></span></div><p class=\"value-origin observed\">Observed provider quota telemetry</p>");
                }
                else
                {
                    html.Append("<dt>Observed usage</dt><dd>Unavailable—not zero</dd><dt>Observed headroom</dt><dd>Unknown</dd></dl>");
                }

                if (window.Projection is { } projection)
                    html.Append("<p class=\"projection\"><strong>Inferred exhaustion</strong> ").Append(Time(projection.ProjectedExhaustionAtUtc))
                        .Append(projection.ExhaustsBeforeReset ? " (before reset)" : " (after reset)")
                        .Append("</p><p class=\"value-origin inferred\">Inferred from ").Append(Number(projection.TokensPerHour))
                        .Append(" tokens/hour over ").Append(Duration(projection.BasedOnTrailingWindow)).Append("; not provider-observed.</p>");
                else
                    html.Append("<p class=\"projection\"><strong>Inferred exhaustion</strong> Unknown</p>");
                html.Append("</article>");
            }
            if (row.QuotaWindows.Count == 0)
                html.Append("<p class=\"missing-quota\">No quota-window observation—headroom unknown.</p>");
            html.Append("</section><section class=\"model-costs\"><h3>Model cost coverage</h3><ul>");
            foreach (var model in row.Cost.Models)
                html.Append("<li><span>").Append(Escape(model.ModelId)).Append("</span><b>")
                    .Append(Escape(ModelCostLabel(model))).Append("</b></li>");
            if (row.Cost.Models.Count == 0)
                html.Append("<li>No model ids supplied—cost cannot be determined.</li>");
            html.Append("</ul></section></article>");
        }
        return html.Append("</div></section>").ToString();
    }

    /// <summary>Renders rows produced by <see cref="ProviderQuotaDashboardBuilder"/> as a visual dashboard fragment.</summary>
    public static string Render(IEnumerable<ProviderQuotaDashboardRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var html = new StringBuilder("<section class=\"provider-quota-dashboard\" aria-label=\"Provider quota dashboard\"><h1>Provider quota dashboard</h1><div class=\"quota-grid\">");
        foreach (var row in rows)
        {
            var state = row.State.ToString().ToLowerInvariant();
            html.Append("<article class=\"quota-card quota-").Append(state).Append("\"><header><h2>").Append(Escape(row.Provider))
                .Append("</h2><span class=\"state ").Append(state).Append("\">").Append(state).Append("</span></header><p class=\"rate\">")
                .Append(Number(row.TokensPerHour)).Append(" <span>tokens/hour</span></p><p class=\"trailing-window\">Trailing consumption window</p>")
                .Append("<div class=\"quota-meter\" role=\"progressbar\" aria-label=\"").Append(Escape(row.Provider)).Append(" quota mark used\" aria-valuenow=\"").Append(Number(row.QuotaMarkPercent)).Append("\" aria-valuemin=\"0\" aria-valuemax=\"100\"><span style=\"width:").Append(Number(row.QuotaMarkPercent)).Append("%\"></span></div>")
                .Append("<dl><dt>Quota mark</dt><dd>").Append(Number(row.QuotaWindowTokens)).Append(" / ").Append(Number(row.QuotaMarkTokens)).Append(" tokens (").Append(Number(row.QuotaMarkPercent)).Append("%)</dd>")
                .Append("<dt>Until mark</dt><dd>").Append(Number(row.TokensUntilMark)).Append(" tokens</dd><dt>Projected mark</dt><dd>").Append(row.ProjectedMarkAtUtc is { } projected ? projected.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "No recent rate")
                .Append("</dd></dl><section class=\"tier-shares\"><h3>Active-window tier share</h3><ul>");
            foreach (var share in row.ModelShares)
                html.Append("<li><span>").Append(Escape(share.Tier)).Append("</span><span class=\"tier-bar\"><i style=\"width:").Append(Number(share.Percent)).Append("%\"></i></span><b>").Append(Number(share.Percent)).Append("%</b></li>");
            if (!row.ModelShares.Any())
                html.Append("<li class=\"no-tier-consumption\">No active-window consumption</li>");
            html.Append("</ul></section></article>");
        }
        return html.Append("</div></section>").ToString();
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
    private static string Css<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
    private static string Label<T>(T value) where T : struct, Enum => value.ToString();
    private static string CostLabel(SnapshotCostStatus status) => status switch
    {
        SnapshotCostStatus.Priced => "List price available",
        SnapshotCostStatus.Unconfirmed => "Unconfirmed price",
        SnapshotCostStatus.Unpriced => "Unpriced",
        _ => "Unknown cost",
    };
    private static string ModelCostLabel(ModelCostAtDecision model) => model.Unconfirmed
        ? "Unconfirmed price"
        : model.PriceStatus switch
        {
            PriceStatus.Resolved => "List price available",
            PriceStatus.NoPriceForDate => "Unpriced at decision time",
            PriceStatus.UnknownModel => "Unknown model cost",
            PriceStatus.UsageUnavailable => "Usage unavailable",
            _ => "Unknown cost",
        };
    private static string Time(DateTime value) => $"<time datetime=\"{value.ToString("O", CultureInfo.InvariantCulture)}\">{value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}</time>";
    private static string Duration(TimeSpan value) => value.TotalHours >= 1
        ? $"{Number((decimal)value.TotalHours)} hour(s)"
        : $"{Number((decimal)value.TotalMinutes)} minute(s)";
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
