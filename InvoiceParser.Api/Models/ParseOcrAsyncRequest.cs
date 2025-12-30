namespace InvoiceParser.Api.Models;

public sealed class ParseOcrAsyncRequest
{
    public string? OcrText { get; set; }
    public string? SourceType { get; set; }
}
