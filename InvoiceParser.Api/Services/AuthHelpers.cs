using Microsoft.AspNetCore.Mvc;

namespace InvoiceParser.Api.Services;

public static class AuthHelpers
{
    public static bool IsAuthorized(HttpRequest request, string? expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken)) return true;

        if (!request.Headers.TryGetValue("Authorization", out var h)) return false;
        var value = h.ToString();
        if (string.IsNullOrWhiteSpace(value)) return false;

        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var token = value[prefix.Length..].Trim();
        return !string.IsNullOrEmpty(token) && token == expectedToken;
    }

    public static IActionResult UnauthorizedResult() => new UnauthorizedObjectResult(new { error = "Unauthorized" });
}
