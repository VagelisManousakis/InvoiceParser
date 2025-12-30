namespace InvoiceParser.Api.Services;

public interface IFetchHtmlService
{
    Task<string> FetchHtmlAsync(string url, IDictionary<string, string>? extraHeaders = null, CancellationToken ct = default);
    Task<(string Html, IReadOnlyList<string> SetCookieHeaders)> FetchHtmlAndCookiesAsync(string url, IDictionary<string, string>? extraHeaders = null, CancellationToken ct = default);
}
