namespace InvoiceParser.Api.Models;

using System.Text.Json.Serialization;

public sealed class InvoiceItem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vatAmount")]
    public decimal? VatAmount { get; set; }

    [JsonPropertyName("vatPercent")]
    public decimal? VatPercent { get; set; }

    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; set; }

    [JsonPropertyName("subcategoryName")]
    public string? SubcategoryName { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("expiresInDays")]
    public int? ExpiresInDays { get; set; }

    [JsonPropertyName("invalidatesInDays")]
    public int? InvalidatesInDays { get; set; }
}

