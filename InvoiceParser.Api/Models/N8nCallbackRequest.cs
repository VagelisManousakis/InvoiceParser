namespace InvoiceParser.Api.Models;

public sealed class N8nCallbackRequest
{
    public string? RequestId { get; set; }
    public string? WebhookSecret { get; set; }
    public string? Error { get; set; }

    public string? Vendor { get; set; }
    public string? SourceUrl { get; set; }

    public List<string>? Categories { get; set; }
    public List<InvoiceItem>? Items { get; set; }
}
