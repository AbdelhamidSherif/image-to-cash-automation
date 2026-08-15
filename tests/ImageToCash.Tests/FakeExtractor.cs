using ImageToCash.Core;

namespace ImageToCash.Tests;

public sealed class FakeExtractor : IOrderExtractor
{
    public Task<ExtractionResult> ExtractAsync(string imagePath, CancellationToken ct = default)
    {
        var order = new OrderInfo
        {
            OrderDate = new DateTime(2026, 7, 14),
            ExternalReference = "WEB.2026-0714-A17",
            Debtor = new DebtorInfo { Company = "NorthStar Office GmbH", PaymentMethod = "Bank Transfer" },
            PaymentStatus = PaymentStatus.Paid,
            PaymentDate = new DateTime(2026, 7, 18),
            SourceTotalNet = 1235.00m,
            SourceVat = 234.65m,
            SourceTotal = 1469.65m,
        };
        order.Items.Add(new ItemInfo { Sku = "MAT.OESX.02", Description = "Ergonomic Office Chair", Quantity = 1, UnitNetPrice = 570m, VatPercent = 19m });
        order.Items.Add(new ItemInfo { Sku = "MAT.TBL.01", Description = "Standing Desk", Quantity = 2, UnitNetPrice = 350m, VatPercent = 19m, DiscountPercent = 5m });
        return Task.FromResult(new ExtractionResult { Order = order });
    }
}
