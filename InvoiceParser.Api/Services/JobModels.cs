namespace InvoiceParser.Api.Services;

public enum JobStatus
{
    Processing,
    Completed,
    Failed
}

public sealed class JobRecord
{
    public required string RequestId { get; init; }
    public required JobStatus Status { get; set; }

    public object? Data { get; set; }
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; init; }

    public Dictionary<string, object?> Meta { get; } = new();
}
