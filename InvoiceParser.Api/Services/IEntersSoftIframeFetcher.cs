namespace InvoiceParser.Api.Services;

public interface IEntersSoftIframeFetcher
{
    Task<(string IframeHtml, string IframeUrl)> FetchInvoiceIframeHtmlAsync(string pageUrl, IDictionary<string, string>? extraHeaders = null, CancellationToken ct = default);
}
