using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace InvoiceParser.Api.Services;

public sealed class EntersSoftIframeFetcher : IEntersSoftIframeFetcher
{
    private readonly IFetchHtmlService _fetchHtml;

    public EntersSoftIframeFetcher(IFetchHtmlService fetchHtml)
    {
        _fetchHtml = fetchHtml;
    }

    public async Task<(string IframeHtml, string IframeUrl)> FetchInvoiceIframeHtmlAsync(
        string pageUrl,
        IDictionary<string, string>? extraHeaders = null,
        CancellationToken ct = default)
    {
        var (outerHtml, setCookies) = await _fetchHtml.FetchHtmlAndCookiesAsync(pageUrl, extraHeaders, ct);

        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(outerHtml, ct);

        var iframe = doc.QuerySelector("iframe#iframeContent") ?? doc.QuerySelector("iframe.iframecontent");
        var iframeSrc = iframe?.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(iframeSrc))
            throw new InvalidOperationException("Iframe src not found on page");

        var iframeUrl = new Uri(new Uri(pageUrl), iframeSrc).ToString();

        var cookieHeader = BuildCookieHeader(setCookies);
        var iframeHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (extraHeaders != null)
        {
            foreach (var kv in extraHeaders)
                iframeHeaders[kv.Key] = kv.Value;
        }

        iframeHeaders["Referer"] = pageUrl;
        if (!string.IsNullOrWhiteSpace(cookieHeader))
            iframeHeaders["Cookie"] = cookieHeader;

        var iframeHtml = await _fetchHtml.FetchHtmlAsync(iframeUrl, iframeHeaders, ct);
        return (iframeHtml, iframeUrl);
    }

    private static string? BuildCookieHeader(IReadOnlyList<string> setCookies)
    {
        if (setCookies.Count == 0) return null;
        var parts = new List<string>(setCookies.Count);
        foreach (var c in setCookies)
        {
            var idx = c.IndexOf(';');
            parts.Add(idx >= 0 ? c[..idx] : c);
        }
        return string.Join("; ", parts);
    }
}
