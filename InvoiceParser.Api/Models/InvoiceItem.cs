namespace InvoiceParser.Api.Models;

public sealed class InvoiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public decimal? VatPercent { get; set; }
    public decimal? VatAmount { get; set; }
}
