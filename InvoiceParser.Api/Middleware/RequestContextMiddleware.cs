using System.Globalization;

namespace InvoiceParser.Api.Middleware;

public sealed class RequestContextMiddleware
{
    private static long _counter;
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var rid = CreateRequestId();
        context.Items["requestId"] = rid;
        context.Response.Headers["X-Request-Id"] = rid;

        await _next(context);
    }

    public static string? GetRequestId(HttpContext context)
    {
        if (context.Items.TryGetValue("requestId", out var v) && v is string s) return s;
        return null;
    }

    private static string CreateRequestId()
    {
        var c = Interlocked.Increment(ref _counter);
        var left = ToBase36(c);
        var right = ToBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return left + right;
    }

    private static string ToBase36(long value)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0) return "0";

        var v = value;
        Span<char> buffer = stackalloc char[32];
        var i = buffer.Length;
        while (v > 0)
        {
            var rem = (int)(v % 36);
            buffer[--i] = chars[rem];
            v /= 36;
        }
        return new string(buffer[i..]);
    }
}
