namespace InvoiceParser.Api.Models;

public sealed class ParseHtmlAsyncRequest
{
    public string? Html { get; set; }
    public string? Vendor { get; set; }
    public string? VendorName { get; set; }
    public string? SourceUrl { get; set; }
}
