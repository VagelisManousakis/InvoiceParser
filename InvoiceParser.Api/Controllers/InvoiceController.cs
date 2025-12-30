using InvoiceParser.Api.Middleware;
using InvoiceParser.Api.Models;
using InvoiceParser.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceParser.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class InvoiceController : ControllerBase
{
    private readonly IInvoiceParserService _parser;
    private readonly IN8nClient _n8n;
    private readonly IJobStore _jobs;

    public InvoiceController(IInvoiceParserService parser, IN8nClient n8n, IJobStore jobs)
    {
        _parser = parser;
        _n8n = n8n;
        _jobs = jobs;
    }

    [HttpPost("invoice/parse-url")]
    public async Task<IActionResult> ParseUrl([FromBody] ParseUrlRequest body, CancellationToken ct)
    {
        if (!UrlHelpers.IsValidHttpUrl(body.SourceUrl))
            return BadRequest(new { error = "Invalid sourceUrl. Must be http(s) URL" });

        //var expectedToken = Environment.GetEnvironmentVariable("INVOICE_PARSE_API_KEY");
        //if (!AuthHelpers.IsAuthorized(Request, expectedToken))
        //    return AuthHelpers.UnauthorizedResult();

        var (vendor, items, debug) = await _parser.ParseByUrlAsync(body.SourceUrl!, body.Vendor, body.VendorName, ct);

        //var n8nEnabled = (Environment.GetEnvironmentVariable("N8N_ENABLED") ?? string.Empty).Trim().ToLowerInvariant();
        //var shouldUseN8n = !(string.IsNullOrEmpty(n8nEnabled) || n8nEnabled is "0" or "false" or "off");

        if (true)
        {
            var webhookUrl = Environment.GetEnvironmentVariable("N8N_RECEIPT_WEBHOOK_URL");
            if (!string.IsNullOrWhiteSpace(webhookUrl))
            {
                try
                {
                    var resp = await _n8n.CallReceiptFlowAsync(new N8nReceiptRequest
                    {
                        Vendor = vendor.ToString(),
                        VendorName = body.VendorName,
                        SourceUrl = body.SourceUrl,
                        Items = items
                    }, RequestContextMiddleware.GetRequestId(HttpContext), ct);

                    if (resp.Items != null && resp.Items.Count > 0)
                    {
                        return Ok(new
                        {
                            vendor = vendor.ToString(),
                            sourceUrl = body.SourceUrl,
                            items = resp.Items
                        });
                    }

                    object? debugObj;
                    if (debug == null)
                    {
                        debugObj = new { ai = new { applied = false, reason = "N8N_RESPONSE_NO_ITEMS" } };
                    }
                    else
                    {
                        debugObj = new { parse = debug, ai = new { applied = false, reason = "N8N_RESPONSE_NO_ITEMS" } };
                    }

                    return Ok(new
                    {
                        vendor = vendor.ToString(),
                        sourceUrl = body.SourceUrl,
                        items,
                        debug = debugObj
                    });
                }
                catch (Exception ex)
                {
                    object? debugObj;
                    if (debug == null)
                    {
                        debugObj = new { ai = new { applied = false, reason = "N8N_CALL_FAILED", message = ex.Message } };
                    }
                    else
                    {
                        debugObj = new { parse = debug, ai = new { applied = false, reason = "N8N_CALL_FAILED", message = ex.Message } };
                    }

                    return Ok(new
                    {
                        vendor = vendor.ToString(),
                        sourceUrl = body.SourceUrl,
                        items,
                        debug = debugObj
                    });
                }
            }
        }

        return Ok(new
        {
            vendor = vendor.ToString(),
            sourceUrl = body.SourceUrl,
            items,
            debug = items.Count > 0 ? null : debug
        });
    }


    [HttpPost("invoice/parse-async")]
    public async Task<IActionResult> ParseAsync([FromBody] ParseUrlRequest body)
    {
        var requestId = RequestContextMiddleware.GetRequestId(HttpContext) ?? Guid.NewGuid().ToString("N");

        if (!UrlHelpers.IsValidHttpUrl(body.SourceUrl))
            return BadRequest(new { error = "Invalid sourceUrl. Must be http(s) URL" });

        var expectedToken = Environment.GetEnvironmentVariable("INVOICE_PARSE_API_KEY");
        if (!AuthHelpers.IsAuthorized(Request, expectedToken))
            return AuthHelpers.UnauthorizedResult();

        var vendor = _parser.DetectVendor(body.Vendor, body.SourceUrl);

        _jobs.CreateJob(requestId, new Dictionary<string, object?>
        {
            ["vendor"] = vendor.ToString(),
            ["vendorName"] = body.VendorName,
            ["sourceUrl"] = body.SourceUrl
        });

        try
        {
            var (v, items, _) = await _parser.ParseByUrlAsync(
                body.SourceUrl!,
                body.Vendor,
                body.VendorName,
                HttpContext.RequestAborted);

            var n8nEnabled = true;

            //var shouldUseN8n = !(string.IsNullOrEmpty(n8nEnabled) || n8nEnabled is "0" or "false" or "off");
            var webhookUrl = "https://n8n.srv1131206.hstgr.cloud/webhook-test/new-receipt";//Environment.GetEnvironmentVariable("N8N_RECEIPT_WEBHOOK_URL");

            if (true && !string.IsNullOrWhiteSpace(webhookUrl))
            {
                await _n8n.FireReceiptFlowAsync(
                    new N8nReceiptRequest
                    {
                        Vendor = v.ToString(),
                        VendorName = body.VendorName,
                        SourceUrl = body.SourceUrl,
                        Items = items
                    },
                    requestId,
                    "https://localsexpert20250128215530.azurewebsites.net/api/webhook/n8n-callback",
                    HttpContext.RequestAborted);
            }



            return Ok(new
            {
                requestId,
                status = "processing",
                message = "Parsing completed; waiting for n8n callback. Poll GET /api/job/:requestId for results."
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { requestId, error = "Request cancelled" });
        }
        catch (Exception ex)
        {

            return StatusCode(500, new { requestId, error = "Failed to start processing", details = ex.Message });
        }
    }

    [HttpPost("invoice/parse-html-async")]
    public async Task<IActionResult> ParseHtmlAsync([FromBody] ParseHtmlAsyncRequest body)
    {
        var requestId = RequestContextMiddleware.GetRequestId(HttpContext) ?? Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(body.Html) || body.Html.Length < 50)
            return BadRequest(new { error = "Missing or invalid html. Must be a string with HTML content." });

       

        var vendor = _parser.DetectVendor(body.Vendor, body.SourceUrl);

        _jobs.CreateJob(requestId, new Dictionary<string, object?>
        {
            ["vendor"] = vendor.ToString(),
            ["vendorName"] = body.VendorName,
            ["sourceUrl"] = body.SourceUrl
        });

        try
        {
            var (v, items, _) = _parser.ParseByHtml(body.Html!, body.Vendor, body.SourceUrl);

            

           // var shouldUseN8n = !(string.IsNullOrEmpty(n8nEnabled) || n8nEnabled is "0" or "false" or "off");

            
            var webhookUrl = "https://n8n.srv1131206.hstgr.cloud/webhook-test/new-receipt";

            if (true && !string.IsNullOrWhiteSpace(webhookUrl))
            {
                await _n8n.FireReceiptFlowAsync(
                    new N8nReceiptRequest
                    {
                        Vendor = v.ToString(),
                        VendorName = body.VendorName,
                        SourceUrl = body.SourceUrl,
                        Items = items
                    },
                    requestId,
                    "https://localsexpert20250128215530.azurewebsites.net/api/webhook/n8n-callback",
                    CancellationToken.None
                );
            }
        }
        catch
        {
            
        }

        return Ok(new
        {
            requestId,
            status = "processing",
            message = "Processing started. Poll GET /api/job/:requestId for results."
        });
    }


    [HttpPost("invoice/parse-ocr-async")]
    public async Task<IActionResult> ParseOcrAsync([FromBody] ParseOcrAsyncRequest body)
    {
        var requestId = RequestContextMiddleware.GetRequestId(HttpContext) ?? Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(body.OcrText))
            return BadRequest(new { error = "Missing or empty ocrText" });

        var expectedToken = Environment.GetEnvironmentVariable("INVOICE_PARSE_API_KEY");
        if (!AuthHelpers.IsAuthorized(Request, expectedToken))
            return AuthHelpers.UnauthorizedResult();

        _jobs.CreateJob(requestId, new Dictionary<string, object?>
        {
            ["sourceType"] = body.SourceType ?? "receipt_photo",
            ["ocrTextLength"] = body.OcrText.Length
        });

        try
        {
           

            
            var webhookUrl = "https://n8n.srv1131206.hstgr.cloud/webhook-test/new-receipt";

            if (true && !string.IsNullOrWhiteSpace(webhookUrl))
            {
                await _n8n.FireOcrFlowAsync(
                    new N8nOcrRequest
                    {
                        OcrText = body.OcrText!,
                        SourceType = body.SourceType ?? "receipt_photo"
                    },
                    requestId,
                    "https://localsexpert20250128215530.azurewebsites.net/api/webhook/n8n-callback",
                    CancellationToken.None
                );
            }
        }
        catch
        {
        }

        return Ok(new
        {
            requestId,
            status = "processing",
            message = "Processing started. Poll GET /api/job/:requestId for results."
        });
    }
}
