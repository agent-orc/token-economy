using System.Globalization;
using System.Net;
using System.Text;

namespace TokenEconomy;

/// <summary>Renders an accessible, dependency-free provider quota dashboard view.</summary>
public static class ProviderQuotaDashboardHtmlRenderer
{
    /// <summary>Renders rows produced by <see cref="ProviderQuotaDashboardBuilder"/> as a complete HTML fragment.</summary>
    public static string Render(IEnumerable<ProviderQuotaDashboardRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var html = new StringBuilder("<section class=\"provider-quota-dashboard\" aria-label=\"Provider quota dashboard\"><h1>Provider quota dashboard</h1><table><thead><tr><th>Provider</th><th>Consumption rate</th><th>Quota mark</th><th>Until mark</th><th>Projected mark</th><th>Tier share</th><th>State</th></tr></thead><tbody>");
        foreach (var row in rows)
        {
            var state = row.State.ToString().ToLowerInvariant();
            html.Append("<tr class=\"quota-").Append(state).Append("\"><th scope=\"row\">").Append(Escape(row.Provider))
                .Append("</th><td>").Append(Number(row.TokensPerHour)).Append(" tokens/hour</td><td>").Append(Number(row.QuotaMarkPercent)).Append("% of ").Append(Number(row.QuotaMarkTokens))
                .Append("</td><td>").Append(Number(row.TokensUntilMark)).Append(" tokens</td><td>").Append(row.ProjectedMarkAtUtc is { } projected ? projected.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "No recent rate")
                .Append("</td><td><ul>");
            foreach (var share in row.ModelShares)
                html.Append("<li>").Append(Escape(share.Tier)).Append(": ").Append(Number(share.Percent)).Append("% (").Append(Number(share.Tokens)).Append(")</li>");
            html.Append("</ul></td><td><span class=\"state ").Append(state).Append("\">").Append(state).Append("</span></td></tr>");
        }
        return html.Append("</tbody></table></section>").ToString();
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
