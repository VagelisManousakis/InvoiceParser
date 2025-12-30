using System.Net.Http.Json;

namespace InvoiceParser.Api.Services;

public sealed class N8nClient : IN8nClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public N8nClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<N8nReceiptResponse> CallReceiptFlowAsync(N8nReceiptRequest payload, string? requestId, CancellationToken ct = default)
    {
        var url = Environment.GetEnvironmentVariable("N8N_RECEIPT_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("N8N_RECEIPT_WEBHOOK_URL is not set");

        var timeoutMsStr = Environment.GetEnvironmentVariable("N8N_TIMEOUT_MS") ?? "12000";
        _ = int.TryParse(timeoutMsStr, out var timeoutMs);
        if (timeoutMs <= 0) timeoutMs = 12000;

        var secret = Environment.GetEnvironmentVariable("N8N_WEBHOOK_SECRET");

        var client = _httpClientFactory.CreateClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(payload);
        if (!string.IsNullOrWhiteSpace(secret)) req.Headers.TryAddWithoutValidation("X-Webhook-Secret", secret);
        if (!string.IsNullOrWhiteSpace(requestId)) req.Headers.TryAddWithoutValidation("X-Request-Id", requestId);

        using var resp = await client.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();

        var data = await resp.Content.ReadFromJsonAsync<N8nReceiptResponse>(cancellationToken: cts.Token);
        return data ?? new N8nReceiptResponse();
    }

    public async Task FireReceiptFlowAsync(N8nReceiptRequest payload, string requestId, string? callbackUrl, CancellationToken ct = default)
    {
        var url = "https://n8n.srv1131206.hstgr.cloud/webhook-test/new-receipt";//Environment.GetEnvironmentVariable("N8N_RECEIPT_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("N8N_RECEIPT_WEBHOOK_URL is not set");

        //var secret = Environment.GetEnvironmentVariable("N8N_WEBHOOK_SECRET");
        var client = _httpClientFactory.CreateClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var asyncPayload = new Dictionary<string, object?>
        {
            ["vendor"] = payload.Vendor,
            ["vendorName"] = payload.VendorName,
            ["sourceUrl"] = payload.SourceUrl,
            ["items"] = payload.Items,
            ["requestId"] = requestId,
            ["callbackUrl"] = callbackUrl ?? Environment.GetEnvironmentVariable("N8N_CALLBACK_URL")
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(asyncPayload);
        req.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        //if (!string.IsNullOrWhiteSpace(secret)) req.Headers.TryAddWithoutValidation("X-Webhook-Secret", secret);

        using var resp = await client.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();
    }

    public async Task FireOcrFlowAsync(N8nOcrRequest payload, string requestId, string? callbackUrl, CancellationToken ct = default)
    {
        var url = Environment.GetEnvironmentVariable("N8N_RECEIPT_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("N8N_RECEIPT_WEBHOOK_URL is not set");

        var secret = Environment.GetEnvironmentVariable("N8N_WEBHOOK_SECRET");
        var client = _httpClientFactory.CreateClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var asyncPayload = new Dictionary<string, object?>
        {
            ["type"] = "ocr_receipt",
            ["ocrText"] = payload.OcrText,
            ["sourceType"] = payload.SourceType,
            ["requestId"] = requestId,
            ["callbackUrl"] = callbackUrl ?? Environment.GetEnvironmentVariable("N8N_CALLBACK_URL")
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(asyncPayload);
        req.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        if (!string.IsNullOrWhiteSpace(secret)) req.Headers.TryAddWithoutValidation("X-Webhook-Secret", secret);

        using var resp = await client.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();
    }
}
