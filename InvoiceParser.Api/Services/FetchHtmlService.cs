using System.Net;

namespace InvoiceParser.Api.Services;

public sealed class FetchHtmlService : IFetchHtmlService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FetchHtmlService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> FetchHtmlAsync(string url, IDictionary<string, string>? extraHeaders = null, CancellationToken ct = default)
    {
        var (html, _) = await FetchInternalAsync(url, extraHeaders, ct);
        return html;
    }

    public async Task<(string Html, IReadOnlyList<string> SetCookieHeaders)> FetchHtmlAndCookiesAsync(
        string url,
        IDictionary<string, string>? extraHeaders = null,
        CancellationToken ct = default)
    {
        return await FetchInternalAsync(url, extraHeaders, ct);
    }

    private async Task<(string Html, IReadOnlyList<string> SetCookieHeaders)> FetchInternalAsync(
        string url,
        IDictionary<string, string>? extraHeaders,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyDefaultHeaders(req);
        if (extraHeaders != null)
        {
            foreach (var kv in extraHeaders)
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if ((int)resp.StatusCode < 200 || (int)resp.StatusCode >= 400)
        {
            var status = (int)resp.StatusCode;
            var statusText = resp.ReasonPhrase ?? "Request failed";
            throw new HttpRequestException($"Upstream fetch failed: {status} {statusText}", null, resp.StatusCode);
        }

        var setCookies = resp.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToArray()
            : Array.Empty<string>();

        var html = await resp.Content.ReadAsStringAsync(ct);
        return (html, setCookies);
    }

    private static void ApplyDefaultHeaders(HttpRequestMessage req)
    {
        req.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        req.Headers.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        req.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        req.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        req.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
    }
}
