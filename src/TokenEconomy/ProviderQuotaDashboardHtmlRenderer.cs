using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

/// <summary>Renders an accessible, dependency-free provider quota dashboard view.</summary>
public static class ProviderQuotaDashboardHtmlRenderer
{
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
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
