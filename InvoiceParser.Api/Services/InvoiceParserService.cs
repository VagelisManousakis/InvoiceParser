using InvoiceParser.Api.Models;

namespace InvoiceParser.Api.Services;

public sealed class InvoiceParserService : IInvoiceParserService
{
    private readonly IFetchHtmlService _fetchHtml;
    private readonly IEntersSoftIframeFetcher _iframeFetcher;

    public InvoiceParserService(IFetchHtmlService fetchHtml, IEntersSoftIframeFetcher iframeFetcher)
    {
        _fetchHtml = fetchHtml;
        _iframeFetcher = iframeFetcher;
    }

    public Vendor DetectVendor(string? vendor, string? sourceUrl)
        => HtmlInvoiceParsers.DetectVendor(vendor, sourceUrl);

    public async Task<(Vendor Vendor, List<InvoiceItem> Items, object? Debug)> ParseByUrlAsync(
        string sourceUrl,
        string? vendorIn,
        string? vendorName,
        CancellationToken ct)
    {
        var vendor = HtmlInvoiceParsers.DetectVendor(vendorIn, sourceUrl);

        if (vendor == Vendor.ENTERSOFT)
        {
            var timeoutMsStr = Environment.GetEnvironmentVariable("ENTERSOFT_IFRAME_TIMEOUT_MS") ?? "12000";
            _ = int.TryParse(timeoutMsStr, out var timeoutMs);
            if (timeoutMs <= 0) timeoutMs = 12000;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            try
            {
                var (iframeHtml, iframeUrl) = await _iframeFetcher.FetchInvoiceIframeHtmlAsync(sourceUrl, null, cts.Token);
                var (items, debug) = HtmlInvoiceParsers.ParseGenericItemsFromHtml(iframeHtml);
                if (items.Count > 0)
                    return (vendor, items, null);

                return (vendor, items, new { vendor = vendor.ToString(), vendorName, reason = "ENTERSOFT_NO_ITEMS", iframeUrl, parse = debug });
            }
            catch (Exception ex)
            {
                return (vendor, new List<InvoiceItem>(), new { vendor = vendor.ToString(), vendorName, reason = "ENTERSOFT_FETCH_OR_PARSE_FAILED", message = ex.Message });
            }
        }

        var fetchTimeoutMsStr = Environment.GetEnvironmentVariable("URL_FETCH_TIMEOUT_MS") ?? "15000";
        _ = int.TryParse(fetchTimeoutMsStr, out var fetchTimeoutMs);
        if (fetchTimeoutMs <= 0) fetchTimeoutMs = 15000;

        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(TimeSpan.FromMilliseconds(fetchTimeoutMs));
            string html;
            try
            {
                html = await _fetchHtml.FetchHtmlAsync(sourceUrl, null, cts.Token);
            }
            catch (Exception ex)
            {
                return (vendor, new List<InvoiceItem>(), new { reason = "URL_FETCH_FAILED", message = ex.Message });
            }

            if (vendor == Vendor.EPSILON_DIGITAL)
            {
                var (epsItems, epsDebug) = HtmlInvoiceParsers.ParseEpsilonDigitalItemsFromHtml(html);
                if (epsItems.Count > 0)
                    return (vendor, epsItems, null);

                var (fallbackItems, fallbackDebug) = HtmlInvoiceParsers.ParseGenericItemsFromHtml(html);
                if (fallbackItems.Count > 0)
                {
                    return (vendor, fallbackItems, new { path = "EPSILON_DIGITAL_FALLBACK_GENERIC", epsilon = epsDebug, generic = fallbackDebug });
                }

                return (vendor, new List<InvoiceItem>(), new { path = "EPSILON_DIGITAL_NO_ITEMS", epsilon = epsDebug, generic = fallbackDebug });
            }

            var (items2, debug2) = HtmlInvoiceParsers.ParseGenericItemsFromHtml(html);
            if (items2.Count > 0)
                return (vendor, items2, null);

            return (vendor, items2, debug2);
        }
    }

    public (Vendor Vendor, List<InvoiceItem> Items, object? Debug) ParseByHtml(string html, string? vendorIn, string? sourceUrl)
    {
        var vendor = HtmlInvoiceParsers.DetectVendor(vendorIn, sourceUrl);

        if (vendor == Vendor.EPSILON_DIGITAL)
        {
            var (epsItems, epsDebug) = HtmlInvoiceParsers.ParseEpsilonDigitalItemsFromHtml(html);
            if (epsItems.Count > 0)
                return (vendor, epsItems, null);

            var (fallbackItems, fallbackDebug) = HtmlInvoiceParsers.ParseGenericItemsFromHtml(html);
            if (fallbackItems.Count > 0)
                return (vendor, fallbackItems, new { path = "EPSILON_DIGITAL_FALLBACK_GENERIC", epsilon = epsDebug, generic = fallbackDebug });

            return (vendor, new List<InvoiceItem>(), new { path = "EPSILON_DIGITAL_NO_ITEMS", epsilon = epsDebug, generic = fallbackDebug });
        }

        var (items, debug) = HtmlInvoiceParsers.ParseGenericItemsFromHtml(html);
        return items.Count > 0 ? (vendor, items, null) : (vendor, items, debug);
    }
}
