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

    //[HttpPost("webhook/n8n-callback")]
    //public IActionResult HandleN8nCallback([FromBody] N8nCallbackRequest body)
    //{
    //    var requestId = body.RequestId ?? (Request.Headers.TryGetValue("X-Request-Id", out var h) ? h.ToString() : null);
    //    if (string.IsNullOrWhiteSpace(requestId))
    //        return BadRequest(new { error = "Missing requestId" });


    //    if (!string.IsNullOrWhiteSpace(body.Error))
    //    {
    //        _jobs.FailJob(requestId, body.Error);
    //        return Ok(new { success = true, requestId, status = "failed" });
    //    }

    //    var jobData = new
    //    {
    //        categories = body.Categories ?? new List<string>(),
    //        items = body.Items ?? new List<InvoiceItem>(),
    //        vendor = body.Vendor,
    //        sourceUrl = body.SourceUrl
    //    };

    //    _jobs.CompleteJob(requestId, jobData);
    //    return Ok(new { success = true, requestId, status = "completed" });
    //}

   

[HttpPost("webhook/n8n-callback")]
public IActionResult HandleN8nCallback([FromBody] JsonElement body)
{
    // RequestId: from body (if exists) OR header X-Request-Id
    string? requestId =
        TryGetString(body, "requestId")
        ?? (Request.Headers.TryGetValue("X-Request-Id", out var h) ? h.ToString() : null);

    if (string.IsNullOrWhiteSpace(requestId))
        return BadRequest(new { error = "Missing requestId" });

    // If n8n sends error either at root object, or inside array elements
    var normalized = NormalizePayload(body);

    if (!string.IsNullOrWhiteSpace(normalized.Error))
    {
        _jobs.FailJob(requestId, normalized.Error);
        return Ok(new { success = true, requestId, status = "failed" });
    }

    var jobData = new
    {
        categories = normalized.Categories ?? new List<N8nCategory>(),
        items = normalized.Items ?? new List<InvoiceItem>(),
        vendor = normalized.Vendor,
        sourceUrl = normalized.SourceUrl
    };

    _jobs.CompleteJob(requestId, jobData);
    return Ok(new { success = true, requestId, status = "completed" });
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
