using System.Globalization;
using System.Text.RegularExpressions;
using ImageToCash.Core;

namespace ImageToCash.Extraction;

/// <summary>
/// Layout-relative extraction: detects labeled fields and the items-table column
/// anchors from the OCR output itself rather than relying on absolute coordinates.
/// </summary>
public sealed class HeuristicOrderExtractor : IOrderExtractor
{
    private readonly IOcrEngine _ocr;

    public HeuristicOrderExtractor(IOcrEngine ocr) => _ocr = ocr;

    public async Task<ExtractionResult> ExtractAsync(string imagePath, CancellationToken ct = default)
    {
        var lines = await _ocr.RecognizeAsync(imagePath, ct);
        var result = new Normalizer(lines).Normalize();
        result.RawText = string.Join("\n", lines.Select(l => l.Text));
        return result;
    }

    internal static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = Regex.Replace(s, @"[^\d.,-]", "");
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;
        if (decimal.TryParse(cleaned.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out d))
            return d;
        return null;
    }

    private sealed class Normalizer
    {
        private readonly List<Line> _lines;

        public Normalizer(IReadOnlyList<OcrLine> rawLines)
        {
            _lines = rawLines
                .Select(l => new Line(l.Words.ToList(), l.Words[0].Y))
                .ToList();
        }

        public ExtractionResult Normalize()
        {
            var result = new ExtractionResult();
            var order = result.Order;

            foreach (var line in _lines)
            {
                var t = line.Text;
                var lower = t.ToLowerInvariant();
                if (lower.StartsWith("order date") || lower.StartsWith("date:"))
                {
                    var v = AfterColon(t);
                    if (TryDate(v, out var d)) order.OrderDate = d;
                }
                else if (lower.StartsWith("external reference"))
                {
                    var v = AfterColon(t);
                    if (!string.IsNullOrWhiteSpace(v)) order.ExternalReference = v;
                }
                else if (lower.StartsWith("company:"))
                {
                    order.Debtor.Company = AfterColon(t);
                }
                else if (lower.StartsWith("name:"))
                {
                    SplitName(AfterColon(t), order.Debtor);
                }
                else if (lower.StartsWith("alias:"))
                {
                    order.Debtor.Alias = AfterColon(t);
                }
                else if (lower.StartsWith("address:"))
                {
                    order.Debtor.InvoiceAddress = ParseAddress(AfterColon(t));
                }
                else if (lower.StartsWith("email:"))
                {
                    (order.Debtor.InvoiceAddress ??= new AddressInfo()).Email = AfterColon(t);
                }
                else if (lower.StartsWith("phone:"))
                {
                    (order.Debtor.InvoiceAddress ??= new AddressInfo()).Phone = AfterColon(t);
                }
                else if (lower.StartsWith("payment") && ContainsAny(t, "Bank Transfer", "Credit Card", "SEPA", "bank"))
                {
                    var v = t.Remove(0, "payment".Length).TrimStart(':', ' ', '-').Trim();
                    order.Debtor.PaymentMethod = v;
                    order.Debtor.PaymentMethodCode = MapPaymentCode(v);
                }
                else if (lower.StartsWith("payment status"))
                {
                    var v = AfterColon(t);
                    order.PaymentStatus = v.Equals("PAID", StringComparison.OrdinalIgnoreCase)
                        ? PaymentStatus.Paid
                        : v.Equals("UNPAID", StringComparison.OrdinalIgnoreCase)
                            ? PaymentStatus.Unpaid
                            : PaymentStatus.Unspecified;
                }
                else if (lower.StartsWith("payment date"))
                {
                    if (TryDate(AfterColon(t), out var d)) order.PaymentDate = d;
                }
                else if (lower.StartsWith("net total"))
                {
                    if (ParseDecimal(AfterColon(t)) is { } p) order.SourceTotalNet = p;
                }
                else if (lower.StartsWith("vat") && t.Contains(':'))
                {
                    if (ParseDecimal(AfterColon(t)) is { } p) order.SourceVat = p;
                }
                else if (lower.StartsWith("total") && t.Contains(':'))
                {
                    if (ParseDecimal(AfterColon(t)) is { } p) order.SourceTotal = p;
                }
            }

            var custId = _lines
                .Select(l => l.Text)
                .FirstOrDefault(t => Regex.IsMatch(t, @"cusT-?\d+", RegexOptions.IgnoreCase));
            if (custId is not null)
            {
                var m = Regex.Match(custId, @"cusT-?\d+", RegexOptions.IgnoreCase);
                if (order.Debtor.Alias is null) order.Debtor.Alias = m.Value;
            }

            ParseItemsTable(order, result.Warnings);
            VerifyTotals(order, result.Warnings);
            return result;
        }

        private void ParseItemsTable(OrderInfo order, List<string> warnings)
        {
            var header = _lines.FirstOrDefault(l => l.Words.Any(w => w.Text == "SKU"));
            if (header is null)
            {
                warnings.Add("Items table header (SKU) not found; no items extracted.");
                return;
            }

            var headerY = header.Y;
            var anchor = new Dictionary<string, double>();
            foreach (var line in _lines.Where(l => Math.Abs(l.Y - headerY) < 4))
            {
                foreach (var w in line.Words)
                {
                    switch (w.Text)
                    {
                        case "SKU": anchor.TryAdd("sku", w.X); break;
                        case "Description": anchor.TryAdd("desc", w.X); break;
                        case "Qty": anchor.TryAdd("qty", w.X); break;
                        case "Unit": anchor.TryAdd("unit", w.X); break;
                        case "VAT": anchor.TryAdd("vat", w.X); break;
                        case "Disc": anchor.TryAdd("disc", w.X); break;
                        case "Line": anchor.TryAdd("line", w.X); break;
                    }
                }
            }

            var columns = new[] { "sku", "desc", "qty", "unit", "vat", "disc", "line" };
            if (columns.Any(c => !anchor.ContainsKey(c)))
            {
                warnings.Add("Items table missing one or more column anchors; no items extracted.");
                return;
            }

            var totalsY = _lines
                .Where(l => l.Text.StartsWith("Net total", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Y)
                .DefaultIfEmpty(double.MaxValue)
                .First();

            var bodyWords = _lines
                .Where(l => l.Y > headerY + 2 && l.Y < totalsY)
                .SelectMany(l => l.Words)
                .OrderBy(w => w.Y)
                .ToList();

            foreach (var row in ClusterRows(bodyWords))
            {
                var buckets = columns.ToDictionary(c => c, _ => new List<string>());
                foreach (var w in row)
                {
                    var best = columns.OrderBy(c => Math.Abs(w.X - anchor[c])).First();
                    buckets[best].Add(w.Text);
                }

                var sku = Join(buckets["sku"]);
                var desc = Join(buckets["desc"]);
                var qty = ParseDecimal(buckets["qty"].FirstOrDefault());
                var unit = ParseDecimal(buckets["unit"].FirstOrDefault());
                var vat = ParseDecimal(buckets["vat"].FirstOrDefault());
                var disc = ParseDecimal(buckets["disc"].FirstOrDefault());
                var lineTotal = ParseDecimal(buckets["line"].FirstOrDefault());

                if (string.IsNullOrEmpty(sku) && string.IsNullOrEmpty(desc) && qty is null && unit is null)
                    continue;

                var resolvedQty = qty;
                if (resolvedQty is null && unit is { } u && u > 0 && lineTotal is { } lt)
                {
                    var unitDiscounted = u * (1 - (disc ?? 0) / 100m);
                    if (unitDiscounted > 0)
                        resolvedQty = decimal.Round(lt / unitDiscounted, 2, MidpointRounding.AwayFromZero);
                }

                var item = new ItemInfo
                {
                    Sku = sku,
                    Description = desc,
                    Quantity = resolvedQty ?? 0,
                    UnitNetPrice = unit ?? 0,
                    VatPercent = vat ?? 0,
                    DiscountPercent = disc ?? 0,
                    SourceTotal = lineTotal,
                };
                if (string.IsNullOrEmpty(item.Sku))
                    warnings.Add($"Item line missing SKU: '{Join(row.Select(w => w.Text))}'");
                if (string.IsNullOrEmpty(item.Description))
                    warnings.Add($"Item line missing description: '{Join(row.Select(w => w.Text))}'");
                if (qty is null && resolvedQty is not null)
                    warnings.Add($"Item '{item.Sku}' quantity not read by OCR; reconstructed from line total ({resolvedQty}).");
                else if (qty is null)
                    warnings.Add($"Item '{item.Sku}' quantity could not be determined.");
                order.Items.Add(item);
            }

            if (order.Items.Count == 0)
                warnings.Add("No item rows extracted from the items table.");
        }

        private static IEnumerable<List<OcrWord>> ClusterRows(List<OcrWord> words)
        {
            const double yTolerance = 18;
            var current = new List<OcrWord>();
            double currentY = words.Count > 0 ? words[0].Y : 0;
            foreach (var w in words)
            {
                if (current.Count > 0 && Math.Abs(w.Y - currentY) > yTolerance)
                {
                    yield return current;
                    current = new List<OcrWord>();
                    currentY = w.Y;
                }
                current.Add(w);
            }
            if (current.Count > 0) yield return current;
        }

        private static void VerifyTotals(OrderInfo order, List<string> warnings)
        {
            if (order.Items.Count == 0) return;
            var net = order.ComputedNetTotal;
            var vat = order.ComputedVatTotal;
            var total = order.ComputedGrossTotal;

            if (order.SourceTotalNet.HasValue && Math.Abs(order.SourceTotalNet.Value - net) > 0.01m)
                warnings.Add($"Net total mismatch: source {order.SourceTotalNet} vs computed {net}");
            if (order.SourceVat.HasValue && Math.Abs(order.SourceVat.Value - vat) > 0.01m)
                warnings.Add($"VAT mismatch: source {order.SourceVat} vs computed {vat}");
            if (order.SourceTotal.HasValue && Math.Abs(order.SourceTotal.Value - total) > 0.01m)
                warnings.Add($"Total mismatch: source {order.SourceTotal} vs computed {total}");
        }

        private static string MapPaymentCode(string? method) =>
            method switch
            {
                _ when method?.Contains("Bank Transfer", StringComparison.OrdinalIgnoreCase) == true => "Credit transfer",
                _ when method?.Contains("Credit Card", StringComparison.OrdinalIgnoreCase) == true => "Credit card",
                _ when method?.Contains("SEPA", StringComparison.OrdinalIgnoreCase) == true => "SEPA direct debit",
                _ => string.Empty,
            };

        private static bool ContainsAny(string text, params string[] tokens)
        {
            foreach (var t in tokens)
                if (text.Contains(t, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string AfterColon(string line)
        {
            var i = line.IndexOf(':');
            return i < 0 ? line.Trim() : line[(i + 1)..].Trim();
        }

        private static string Join(IEnumerable<string> words) => string.Join(" ", words).Trim();

        private static bool TryDate(string s, out DateTime d)
        {
            s = s.Trim();
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return true;
            if (DateTime.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return true;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        private static void SplitName(string full, DebtorInfo debtor)
        {
            if (string.IsNullOrWhiteSpace(full)) return;
            var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            debtor.FirstName = parts[0];
            debtor.LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
        }

        private static AddressInfo ParseAddress(string address)
        {
            var a = new AddressInfo();
            var m = Regex.Match(address, @"(\d{4,5})\s+(.+)");
            if (m.Success)
            {
                a.Zip = m.Groups[1].Value;
                a.City = m.Groups[2].Value.TrimEnd('.');
            }
            else
            {
                a.City = address;
            }
            return a;
        }

        private sealed class Line
        {
            public Line(List<OcrWord> words, double y)
            {
                Words = words;
                Y = y;
                Text = string.Join(" ", words.Select(w => w.Text));
            }
            public List<OcrWord> Words { get; }
            public double Y { get; }
            public string Text { get; }
        }
    }
}
