using InvoiceParser.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceParser.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobStore _jobs;

    public JobsController(IJobStore jobs)
    {
        _jobs = jobs;
    }

    [HttpGet("job/{requestId}")]
    public IActionResult GetJobStatus([FromRoute] string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return BadRequest(new { error = "Missing requestId" });

        var job = _jobs.GetJob(requestId);
        if (job == null)
        {
            return NotFound(new
            {
                error = "Job not found",
                requestId,
                message = "Job may have expired or does not exist"
            });
        }

        if (job.Status == JobStatus.Processing)
        {
            return Ok(new
            {
                requestId,
                status = "processing",
                createdAt = job.CreatedAt.ToUnixTimeMilliseconds(),
                message = "Processing receipt with AI..."
            });
        }

        if (job.Status == JobStatus.Failed)
        {
            return Ok(new
            {
                requestId,
                status = "failed",
                error = job.Error,
                createdAt = job.CreatedAt.ToUnixTimeMilliseconds(),
                updatedAt = job.UpdatedAt.ToUnixTimeMilliseconds()
            });
        }

        return Ok(new
        {
            requestId,
            status = "completed",
            data = job.Data,
            createdAt = job.CreatedAt.ToUnixTimeMilliseconds(),
            updatedAt = job.UpdatedAt.ToUnixTimeMilliseconds()
        });
    }
}
