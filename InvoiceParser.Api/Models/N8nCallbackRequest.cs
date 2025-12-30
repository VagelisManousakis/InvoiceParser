namespace InvoiceParser.Api.Models;

public sealed class N8nCallbackEnvelope
{
    // Optional if n8n sends them (your sample doesn't)
    public string? RequestId { get; set; }
    public string? WebhookSecret { get; set; }
    public string? Error { get; set; }

    public string? Vendor { get; set; }
    public string? SourceUrl { get; set; }

    public List<N8nCategory>? Categories { get; set; }
    public List<InvoiceItem>? Items { get; set; }
}

public sealed class N8nCategory
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public N8nIcon? Icon { get; set; }
    public string? Color { get; set; }
}

public sealed class N8nIcon
{
    public string? Library { get; set; }
    public string? Name { get; set; }
}

