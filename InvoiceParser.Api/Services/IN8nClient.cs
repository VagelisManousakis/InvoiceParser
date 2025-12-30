namespace InvoiceParser.Api.Services;

public interface IN8nClient
{
    Task<N8nReceiptResponse> CallReceiptFlowAsync(N8nReceiptRequest payload, string? requestId, CancellationToken ct = default);
    Task FireReceiptFlowAsync(N8nReceiptRequest payload, string requestId, string? callbackUrl, CancellationToken ct = default);
    Task FireOcrFlowAsync(N8nOcrRequest payload, string requestId, string? callbackUrl, CancellationToken ct = default);
}

public sealed class N8nReceiptRequest
{
    public string? Vendor { get; set; }
    public string? VendorName { get; set; }
    public string? SourceUrl { get; set; }
    public List<Models.InvoiceItem> Items { get; set; } = new();
}

public sealed class N8nReceiptResponse
{
    public List<Models.InvoiceItem>? Items { get; set; }
}

public sealed class N8nOcrRequest
{
    public string OcrText { get; set; } = string.Empty;
    public string SourceType { get; set; } = "receipt_photo";
}
