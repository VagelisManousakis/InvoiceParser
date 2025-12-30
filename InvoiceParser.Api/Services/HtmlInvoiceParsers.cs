using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using InvoiceParser.Api.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceParser.Api.Services;

public static class HtmlInvoiceParsers
{
    public static Vendor DetectVendor(string? vendor, string? sourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(vendor) && Enum.TryParse<Vendor>(vendor, ignoreCase: true, out var v))
            return v;

        if (string.IsNullOrWhiteSpace(sourceUrl))
            return Vendor.UNKNOWN;

        try
        {
            var host = new Uri(sourceUrl).Host.ToLowerInvariant();
            if (host.Contains("epsilondigital") || host.EndsWith("epsilonnet.gr"))
                return Vendor.EPSILON_DIGITAL;
            if (host.EndsWith("e-invoicing.gr"))
                return Vendor.ENTERSOFT;
            return Vendor.UNKNOWN;
        }
        catch
        {
            return Vendor.UNKNOWN;
        }
    }

    public static decimal? ParseGreekNumber(string? txt)
    {
        if (txt == null) return null;
        var s1 = txt
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace('\u00A0', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(s1)) return null;

        var cleaned = Regex.Replace(s1, "[^0-9,.-]", string.Empty)
            .Replace(".", "", StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);

        // Undo removing decimal point when it's not a thousands separator.
        // The JS version removes only dots that look like thousands separators; we'll approximate:
        // if we removed all dots and there was exactly one dot and <=2 decimals after it, keep it.
        // Since cleaned already removed dots, fallback: try culture parsing on original normalized string.

        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
            return n;

        var normalized = Regex.Replace(s1, @"\s+", " ");
        normalized = normalized.Replace(".", "", StringComparison.Ordinal);
        normalized = normalized.Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n2)
            ? n2
            : null;
    }

    public static (List<InvoiceItem> Items, object Debug) ParseEpsilonDigitalItemsFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return (new List<InvoiceItem>(), new { reason = "missing_html" });

        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);

        var tables = doc.QuerySelectorAll("table");
        var tableCount = tables.Length;

        IElement? table = doc.QuerySelector("table.document-lines-table");
        var strictFound = table != null;

        var header = (string[]?)null;
        var headerRowIndex = -1;
        int? headerScore = null;

        if (table != null)
        {
            var rows = table.QuerySelectorAll("tr");
            for (var i = 0; i < rows.Length && header == null; i++)
            {
                var cells = RowCells(rows[i]);
                if (IsHeaderRow(cells))
                {
                    header = cells;
                    headerRowIndex = i;
                }
            }
        }

        if (header == null)
        {
            var match = FindInvoiceTableByHeader(tables);
            if (match.Table == null)
            {
                return (new List<InvoiceItem>(), new
                {
                    reason = strictFound ? "strict_table_no_header" : "missing_document_lines_table",
                    tableCount,
                    strictSelectorFound = strictFound,
                    bestHeaderScore = match.Score
                });
            }

            table = match.Table;
            header = match.Header;
            headerRowIndex = match.HeaderRowIndex;
            headerScore = match.Score;
        }

        var idx = MapHeaderIndices(header);
        var outItems = new List<InvoiceItem>();

        var allRows = table!.QuerySelectorAll("tr");
        for (var i = 0; i < allRows.Length; i++)
        {
            if (i <= headerRowIndex) continue;

            var cells = RowCells(allRows[i]);
            if (cells.Length == 0) continue;
            if (LooksLikeSummaryRow(cells)) continue;

            var aaTxt = idx.Aa >= 0 ? cells[idx.Aa] : cells[0];
            var aa = ParseGreekNumber(aaTxt);
            if (aa is null) continue;

            var name = idx.Name >= 0 ? cells[idx.Name] : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            outItems.Add(new InvoiceItem
            {
                Name = name,
                Quantity = idx.Quantity >= 0 ? ParseGreekNumber(cells[idx.Quantity]) : null,
                Unit = idx.Unit >= 0 ? cells[idx.Unit] : null,
                Price = idx.Price >= 0 ? ParseGreekNumber(cells[idx.Price]) : null,
                VatPercent = idx.VatPercent >= 0 ? ParseGreekNumber(cells[idx.VatPercent]) : null,
                VatAmount = idx.VatAmount >= 0 ? ParseGreekNumber(cells[idx.VatAmount]) : null
            });
        }

        var dbg = new
        {
            tableCount,
            strictSelectorFound = strictFound,
            headerRowIndex,
            header,
            idx,
            headerScore
        };

        return (outItems, dbg);
    }

    public static (List<InvoiceItem> Items, object Debug) ParseGenericItemsFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return (new List<InvoiceItem>(), new { reason = "missing_html" });

        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);

        var tables = doc.QuerySelectorAll("table");
        if (tables.Length == 0)
            return (new List<InvoiceItem>(), new { reason = "no_tables" });

        var best = PickBestTable(tables);
        if (best == null)
            return (new List<InvoiceItem>(), new { reason = "no_candidate_tables" });

        var rows = best.QuerySelectorAll("tr");
        if (rows.Length == 0)
            return (new List<InvoiceItem>(), new { reason = "candidate_has_no_rows" });

        string[]? header = null;
        var headerRowIndex = -1;
        for (var i = 0; i < rows.Length && header == null; i++)
        {
            var cells = RowCells(rows[i]);
            if (IsHeaderRow(cells))
            {
                header = cells;
                headerRowIndex = i;
            }
        }

        HeaderIdx? idx = header != null ? MapHeaderIndices(header) : null;

        var outItems = new List<InvoiceItem>();
        for (var i = 0; i < rows.Length; i++)
        {
            if (header != null && i <= headerRowIndex) continue;

            var cells = RowCells(rows[i]);
            if (cells.Length == 0) continue;
            if (LooksLikeSummaryRow(cells)) continue;

            string? name = null;
            decimal? quantity = null;
            string? unit = null;
            decimal? price = null;
            decimal? vatPercent = null;
            decimal? vatAmount = null;

            if (idx != null)
            {
                name = idx.Value.Name >= 0 ? cells[idx.Value.Name] : null;
                quantity = idx.Value.Quantity >= 0 ? ParseGreekNumber(cells[idx.Value.Quantity]) : null;
                unit = idx.Value.Unit >= 0 ? cells[idx.Value.Unit] : null;
                price = idx.Value.Price >= 0 ? ParseGreekNumber(cells[idx.Value.Price]) : null;
                vatPercent = idx.Value.VatPercent >= 0 ? ParseGreekNumber(cells[idx.Value.VatPercent]) : null;
                vatAmount = idx.Value.VatAmount >= 0 ? ParseGreekNumber(cells[idx.Value.VatAmount]) : null;
            }
            else
            {
                var hasLetters = cells.Where(c => Regex.IsMatch(c, "[A-Za-zΑ-Ωα-ω]")).ToArray();
                var hasNumbers = cells.Where(c => Regex.IsMatch(c, "[0-9]")).ToArray();
                if (hasLetters.Length == 0 || hasNumbers.Length == 0) continue;

                name = hasLetters[0];
                quantity = ParseGreekNumber(hasNumbers[0]);
                vatPercent = hasNumbers.Length >= 2 ? ParseGreekNumber(hasNumbers[1]) : null;
                vatAmount = hasNumbers.Length >= 3 ? ParseGreekNumber(hasNumbers[2]) : null;
            }

            if (string.IsNullOrWhiteSpace(name)) continue;

            outItems.Add(new InvoiceItem
            {
                Name = name,
                Quantity = quantity,
                Unit = unit,
                Price = price,
                VatPercent = vatPercent,
                VatAmount = vatAmount
            });
        }

        var dbg = new { headerRowIndex, header, idx };
        return (outItems, dbg);
    }

    private static string[] RowCells(IElement row)
    {
        var cells = row.QuerySelectorAll("th,td");
        return cells
            .Select(c => NormalizeWhitespace(c.TextContent))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private static string NormalizeWhitespace(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return Regex.Replace(s, "\\s+", " ").Trim();
    }

    private static string SafeUpper(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();

    private static bool IsHeaderRow(string[] cells)
    {
        var joined = SafeUpper(string.Join(" ", cells));
        return joined.Contains("ΠΕΡΙΓΡΑΦ") || joined.Contains("ΠΟΣΟΤ") || joined.Contains("ΜΟΝΑΔ") || joined.Contains("ΦΠΑ");
    }

    private static bool LooksLikeSummaryRow(string[] cells)
    {
        var joined = SafeUpper(string.Join(" ", cells));
        return joined.Contains("ΣΥΝΟΛ") || joined.Contains("TOTAL") || joined.Contains("ΓΕΝΙΚ") ||
               joined.Contains("ΠΛΗΡΩ") || joined.Contains("ΚΑΘΑΡ") || joined.Contains("ΦΠΑ:");
    }

    private static int HeaderMatchScore(string[] cells)
    {
        var u = SafeUpper(string.Join(" ", cells));
        var score = 0;
        if (u.Contains("A/A") || u.Contains("AA")) score += 2;
        if (u.Contains("ΠΕΡΙΓΡΑΦ")) score += 3;
        if (u.Contains("ΠΟΣΟΤ")) score += 2;
        if (u.Contains("ΜΟΝΑΔ")) score += 1;
        if (u.Contains("ΦΠΑ")) score += 2;
        return score;
    }

    private static (IElement? Table, int HeaderRowIndex, string[] Header, int Score) FindInvoiceTableByHeader(IHtmlCollection<IElement> tables)
    {
        IElement? best = null;
        var bestScore = -1;
        var bestHeaderRowIndex = -1;
        string[]? bestHeader = null;

        foreach (var table in tables)
        {
            var rows = table.QuerySelectorAll("tr");
            if (rows.Length == 0) continue;

            var localBestScore = -1;
            var localHeaderRowIndex = -1;
            string[]? localHeader = null;

            for (var i = 0; i < rows.Length; i++)
            {
                var cells = RowCells(rows[i]);
                if (cells.Length == 0) continue;

                var s = HeaderMatchScore(cells);
                if (s > localBestScore)
                {
                    localBestScore = s;
                    localHeaderRowIndex = i;
                    localHeader = cells;
                }
            }

            if (localBestScore > bestScore)
            {
                bestScore = localBestScore;
                best = table;
                bestHeaderRowIndex = localHeaderRowIndex;
                bestHeader = localHeader;
            }
        }

        if (best == null || bestScore < 4 || bestHeader == null)
            return (null, -1, Array.Empty<string>(), bestScore);

        return (best, bestHeaderRowIndex, bestHeader, bestScore);
    }

    private static IElement? PickBestTable(IHtmlCollection<IElement> tables)
    {
        IElement? best = null;
        var bestScore = -1;

        foreach (var table in tables)
        {
            var rows = table.QuerySelectorAll("tr");
            if (rows.Length == 0) continue;

            var score = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var cells = RowCells(rows[i]);
                if (i == 0 && IsHeaderRow(cells)) score += 10;
                if (cells.Any(c => Regex.IsMatch(c, "\\d"))) score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = table;
            }
        }

        return best;
    }

    private readonly record struct HeaderIdx(int Aa, int Name, int Quantity, int Unit, int Price, int VatPercent, int VatAmount);

    private static HeaderIdx MapHeaderIndices(string[] cells)
    {
        var idx = new HeaderIdx(-1, -1, -1, -1, -1, -1, -1);

        for (var i = 0; i < cells.Length; i++)
        {
            var u = SafeUpper(cells[i]);

            if (u == "A/A" || u.Contains("A/A") || u.Contains("AA")) idx = idx with { Aa = i };
            if (u.Contains("ΠΕΡΙΓΡΑΦ")) idx = idx with { Name = i };
            if (u.Contains("ΠΟΣΟΤ")) idx = idx with { Quantity = i };
            if (u.Contains("ΜΟΝΑΔ")) idx = idx with { Unit = i };

            if (u.Contains("ΤΙΜ") || u.Contains("PRICE") || (u.Contains("ΜΟΝΑΔΑ") && u.Contains("ΤΙΜ")))
                idx = idx with { Price = i };

            if (u.Contains("ΠΟΣΟΣΤ") && u.Contains("ΦΠΑ")) idx = idx with { VatPercent = i };
            if ((u.Contains("ΑΞΙΑ") || u.Contains("ΠΟΣΟ")) && u.Contains("ΦΠΑ")) idx = idx with { VatAmount = i };
        }

        return idx;
    }
}
