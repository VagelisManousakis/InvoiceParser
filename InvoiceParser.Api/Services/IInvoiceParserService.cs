using InvoiceParser.Api.Models;

namespace InvoiceParser.Api.Services;

public interface IInvoiceParserService
{
    Vendor DetectVendor(string? vendor, string? sourceUrl);

    Task<(Vendor Vendor, List<InvoiceItem> Items, object? Debug)> ParseByUrlAsync(
        string sourceUrl,
        string? vendorIn,
        string? vendorName,
        CancellationToken ct);

    (Vendor Vendor, List<InvoiceItem> Items, object? Debug) ParseByHtml(
        string html,
        string? vendorIn,
        string? sourceUrl);
}
