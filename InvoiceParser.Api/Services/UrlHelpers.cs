namespace InvoiceParser.Api.Services;

public static class UrlHelpers
{
    public static bool IsValidHttpUrl(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Uri.TryCreate(s, UriKind.Absolute, out var u) &&
               (u.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
    }
}
