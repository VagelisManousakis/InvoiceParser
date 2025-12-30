using InvoiceParser.Api.Models;
using InvoiceParser.Api.Services;
using Microsoft.AspNetCore.Mvc;

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
    public IActionResult HandleN8nCallback([FromBody] N8nCallbackRequest body)
    {
        var requestId = body.RequestId ?? (Request.Headers.TryGetValue("X-Request-Id", out var h) ? h.ToString() : null);
        if (string.IsNullOrWhiteSpace(requestId))
            return BadRequest(new { error = "Missing requestId" });

        var expectedSecret = Environment.GetEnvironmentVariable("N8N_CALLBACK_SECRET");
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            var provided = Request.Headers.TryGetValue("X-Webhook-Secret", out var hs) ? hs.ToString() : body.WebhookSecret;
            if (string.IsNullOrWhiteSpace(provided) || provided != expectedSecret)
                return Unauthorized(new { error = "Unauthorized" });
        }

        if (!string.IsNullOrWhiteSpace(body.Error))
        {
            _jobs.FailJob(requestId, body.Error);
            return Ok(new { success = true, requestId, status = "failed" });
        }

        var jobData = new
        {
            categories = body.Categories ?? new List<string>(),
            items = body.Items ?? new List<InvoiceItem>(),
            vendor = body.Vendor,
            sourceUrl = body.SourceUrl
        };

        _jobs.CompleteJob(requestId, jobData);
        return Ok(new { success = true, requestId, status = "completed" });
    }
}
