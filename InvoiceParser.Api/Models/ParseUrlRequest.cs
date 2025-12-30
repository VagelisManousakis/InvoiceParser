namespace InvoiceParser.Api.Models;

public sealed class ParseUrlRequest
{
    public string? Vendor { get; set; }
    public string? VendorName { get; set; }
    public string? SourceUrl { get; set; }
}
