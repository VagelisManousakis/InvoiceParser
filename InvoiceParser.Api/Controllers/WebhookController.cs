using AngleSharp.Io;
using InvoiceParser.Api.Models;
using InvoiceParser.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static System.Reflection.Metadata.BlobBuilder;

namespace InvoiceParser.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class WebhookController : ControllerBase
{
    private readonly IJobStore _jobs;

    public WebhookController(IJobStore jobs)
    {
        _jobs = jobs;
    }

[HttpPost("webhook/n8n-callback")]
public IActionResult HandleN8nCallback([FromBody] JsonElement body)
{
    string? requestId =
        TryGetString(body, "requestId")
        ?? (Request.Headers.TryGetValue("X-Request-Id", out var h) ? h.ToString() : null);

    if (string.IsNullOrWhiteSpace(requestId))
        return BadRequest(new { error = "Missing requestId" });

    var normalized = NormalizePayload(body);

    if (!string.IsNullOrWhiteSpace(normalized.Error))
    {
        _jobs.FailJob(requestId, normalized.Error);
        return Ok(new { requestId, status = "failed" });
    }

    var categories = normalized.Categories ?? new List<N8nCategory>();
    var items = normalized.Items ?? new List<InvoiceItem>();

    // Build a lookup of valid category ids
    var categoryIds = new HashSet<string>(
        categories.Where(c => !string.IsNullOrWhiteSpace(c.Id)).Select(c => c.Id!),
        StringComparer.OrdinalIgnoreCase
    );

    // Normalize per-item category/subcategory
    foreach (var i in items)
    {
        // categoryId -> "other" if missing OR not in categories list
        if (string.IsNullOrWhiteSpace(i.CategoryId) || !categoryIds.Contains(i.CategoryId))
            i.CategoryId = "other";

        // subcategoryName -> "other" if missing
        if (string.IsNullOrWhiteSpace(i.SubcategoryName))
            i.SubcategoryName = "other";
    }

    // Ensure "other" category exists if used by any item
    if (items.Any(x => string.Equals(x.CategoryId, "other", StringComparison.OrdinalIgnoreCase))
        && !categoryIds.Contains("other"))
    {
        categories.Add(new N8nCategory
        {
            Id = "other",
            Name = "Other",
            Icon = null,
            Color = null
        });
    }

    var data = new N8nCallbackResponseData
    {
        Categories = categories,
        Items = items,
        Vendor = normalized.Vendor,
        SourceUrl = normalized.SourceUrl
    };

    // timestamps (epoch ms)
    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    var response = new N8nCallbackResponse
    {
        RequestId = requestId,
        Status = "completed",
        Data = data,
        CreatedAt = nowMs,
        UpdatedAt = nowMs
    };

    // Store exactly what you return (optional but recommended)
    _jobs.CompleteJob(requestId, data);

    return Ok(response);
}



private static N8nCallbackEnvelope NormalizePayload(JsonElement body)
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    // NEW format: [ { categories: [...], items: [...] }, ... ]
    if (body.ValueKind == JsonValueKind.Array)
    {
        var result = new N8nCallbackEnvelope
        {
            Categories = new List<N8nCategory>(),
            Items = new List<InvoiceItem>()
        };

        foreach (var el in body.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;

            // read error/vendor/sourceUrl if present in any element
            result.Error ??= TryGetString(el, "error");
            result.Vendor ??= TryGetString(el, "vendor");
            result.SourceUrl ??= TryGetString(el, "sourceUrl");

            // categories
            if (el.TryGetProperty("categories", out var catsEl) && catsEl.ValueKind == JsonValueKind.Array)
            {
                var cats = JsonSerializer.Deserialize<List<N8nCategory>>(catsEl.GetRawText(), options);
                if (cats is { Count: > 0 })
                    result.Categories!.AddRange(cats);
            }

            // items
            if (el.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                var items = JsonSerializer.Deserialize<List<InvoiceItem>>(itemsEl.GetRawText(), options);
                if (items is { Count: > 0 })
                    result.Items!.AddRange(items);
            }
        }

        // optional: de-dup categories by id
        result.Categories = result.Categories?
            .GroupBy(c => c.Id ?? "")
            .Select(g => g.First())
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .ToList();

        return result;
    }

    // OLD format: { requestId, categories: ["..."], items: [...] } OR new-single-object format
    if (body.ValueKind == JsonValueKind.Object)
    {
        // Try deserialize directly to the new envelope first
        var asEnvelope = JsonSerializer.Deserialize<N8nCallbackEnvelope>(body.GetRawText(), options);
        if (asEnvelope != null)
        {
            // Backward compat: if old body had Categories as strings, you can map them if you want.
            // (Only if you still receive List<string> somewhere — otherwise ignore.)
            return asEnvelope;
        }
    }

    return new N8nCallbackEnvelope();
}

private static string? TryGetString(JsonElement obj, string prop)
{
    if (obj.ValueKind != JsonValueKind.Object) return null;
    if (!obj.TryGetProperty(prop, out var el)) return null;
    return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}

}
