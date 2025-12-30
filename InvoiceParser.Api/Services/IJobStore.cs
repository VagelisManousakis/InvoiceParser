namespace InvoiceParser.Api.Services;

public interface IJobStore
{
    JobRecord CreateJob(string requestId, IDictionary<string, object?>? initialMeta = null);
    JobRecord? GetJob(string requestId);
    bool CompleteJob(string requestId, object data);
    bool FailJob(string requestId, string error);
}
