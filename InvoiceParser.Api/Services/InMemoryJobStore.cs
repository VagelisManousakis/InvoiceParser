using System.Collections.Concurrent;

namespace InvoiceParser.Api.Services;

public sealed class InMemoryJobStore : IJobStore, IDisposable
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();
    private readonly Timer _cleanupTimer;

    public InMemoryJobStore()
    {
        _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public JobRecord CreateJob(string requestId, IDictionary<string, object?>? initialMeta = null)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new JobRecord
        {
            RequestId = requestId,
            Status = JobStatus.Processing,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.Add(JobTtl)
        };

        if (initialMeta != null)
        {
            foreach (var kv in initialMeta)
                job.Meta[kv.Key] = kv.Value;
        }

        _jobs[requestId] = job;
        return job;
    }

    public JobRecord? GetJob(string requestId)
    {
        CleanupOneIfExpired(requestId);
        return _jobs.TryGetValue(requestId, out var job) ? job : null;
    }

    public bool CompleteJob(string requestId, object data)
    {
        var now = DateTimeOffset.UtcNow;
        var job = _jobs.GetOrAdd(requestId, id => new JobRecord
        {
            RequestId = id,
            Status = JobStatus.Completed,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.Add(JobTtl)
        });

        job.Status = JobStatus.Completed;
        job.Data = data;
        job.Error = null;
        job.UpdatedAt = now;
        return true;
    }

    public bool FailJob(string requestId, string error)
    {
        if (!_jobs.TryGetValue(requestId, out var job)) return false;
        job.Status = JobStatus.Failed;
        job.Error = error;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _jobs)
        {
            if (kv.Value.ExpiresAt <= now)
                _jobs.TryRemove(kv.Key, out _);
        }
    }

    private void CleanupOneIfExpired(string requestId)
    {
        if (_jobs.TryGetValue(requestId, out var job))
        {
            if (job.ExpiresAt <= DateTimeOffset.UtcNow)
                _jobs.TryRemove(requestId, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}
