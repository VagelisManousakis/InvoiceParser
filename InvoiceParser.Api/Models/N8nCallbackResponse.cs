using System.Text.Json.Serialization;

namespace InvoiceParser.Api.Models
{
    public sealed class N8nCallbackResponse
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = default!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = default!;

        [JsonPropertyName("data")]
        public N8nCallbackResponseData Data { get; set; } = default!;

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }
    }

    public sealed class N8nCallbackResponseData
    {
        [JsonPropertyName("categories")]
        public List<N8nCategory> Categories { get; set; } = new();

        [JsonPropertyName("items")]
        public List<InvoiceItem> Items { get; set; } = new();

        [JsonPropertyName("vendor")]
        public string? Vendor { get; set; }

        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }
    }
}
