using ImageToCash.Core;
using ImageToCash.Extraction;

namespace ImageToCash.Tests;

public class ExtractionTests
{
    private static FakeOcrEngine Sample()
    {
        return new FakeOcrEngine(
            (36, new[] { ("SALES", 41.0), ("ORDER", 146.0) }),
            (108, new[] { ("SYNTHETIC", 41.0), ("WEB.2026-0714-A17", 205.0) }),
            (138, new[] { ("Order", 41.0), ("Date:", 92.0), ("2026-07-14", 139.0) }),
            (164, new[] { ("External", 41.0), ("Reference:", 112.0), ("WEB.2026-0714-A17", 204.0) }),
            (272, new[] { ("Company:", 51.0), ("NorthStar", 138.0), ("Office", 220.0), ("GmbH", 272.0) }),
            (304, new[] { ("Name:", 51.0), ("Marta", 109.0), ("Klein", 160.0) }),
            (336, new[] { ("Alias:", 50.0), ("NORTHSTAR-BERLIN", 100.0) }),
            (368, new[] { ("Address:", 50.0), ("88", 126.0), ("Friedrichstrasse,", 152.0), ("10117", 284.0), ("Berlin,", 338.0), ("Germany", 394.0) }),
            (542, new[] { ("Payment", 51.0), ("Bank", 197.0), ("Transfer", 242.0) }),
            (574, new[] { ("Payment", 51.0), ("Status:", 127.0), ("PAID", 188.0) }),
            (606, new[] { ("Payment", 51.0), ("Date:", 127.0), ("2026-07-18", 174.0) }),
            (710, new[] { ("SKU", 51.0) }),
            (710, new[] { ("Description", 281.0) }),
            (710, new[] { ("Qty", 680.0) }),
            (710, new[] { ("Unit", 761.0), ("net", 798.0) }),
            (710, new[] { ("VAT", 880.0) }),
            (710, new[] { ("Disc", 971.0) }),
            (710, new[] { ("Line", 1061.0), ("total", 1099.0) }),
            (752, new[] { ("MAT.OESX.02", 51.0), ("Ergonomic", 281.0), ("Office", 372.0), ("Chair", 424.0), ("1", 680.0), ("570.00", 761.0), ("19", 882.0), ("0", 971.0), ("570.00", 1061.0) }),
            (794, new[] { ("MAT.TBL.01", 51.0), ("Standing", 281.0), ("Desk", 357.0), ("2", 680.0), ("350.00", 761.0), ("19", 882.0), ("5", 971.0), ("665.00", 1060.0) }),
            (840, new[] { ("Net", 41.0), ("total:", 73.0), ("EUR", 118.0), ("1235.00", 162.0) }),
            (868, new[] { ("VAT", 40.0), ("(19%):", 81.0), ("EUR", 139.0), ("234.65", 181.0) }),
            (896, new[] { ("Total:", 40.0), ("EUR", 91.0), ("1469.65", 135.0) })
        );
    }

    [Fact]
    public async Task ExtractsOrderHeaderFields()
    {
        var ex = new HeuristicOrderExtractor(Sample());
        var r = await ex.ExtractAsync("x.png");
        Assert.Equal(new DateTime(2026, 7, 14), r.Order.OrderDate);
        Assert.Equal("WEB.2026-0714-A17", r.Order.ExternalReference);
    }

    [Fact]
    public async Task ExtractsDebtor()
    {
        var ex = new HeuristicOrderExtractor(Sample());
        var r = await ex.ExtractAsync("x.png");
        Assert.Equal("NorthStar Office GmbH", r.Order.Debtor.Company);
        Assert.Equal("Marta", r.Order.Debtor.FirstName);
        Assert.Equal("Klein", r.Order.Debtor.LastName);
        Assert.Equal("NORTHSTAR-BERLIN", r.Order.Debtor.Alias);
        Assert.Equal("10117", r.Order.Debtor.InvoiceAddress!.Zip);
        Assert.Equal("Berlin, Germany", r.Order.Debtor.InvoiceAddress.City);
    }

    [Fact]
    public async Task ExtractsPayment()
    {
        var ex = new HeuristicOrderExtractor(Sample());
        var r = await ex.ExtractAsync("x.png");
        Assert.Equal("Bank Transfer", r.Order.Debtor.PaymentMethod);
        Assert.Equal("Credit transfer", r.Order.Debtor.PaymentMethodCode);
        Assert.Equal(PaymentStatus.Paid, r.Order.PaymentStatus);
        Assert.Equal(new DateTime(2026, 7, 18), r.Order.PaymentDate);
    }

    [Fact]
    public async Task ExtractsItemsAndTotals()
    {
        var ex = new HeuristicOrderExtractor(Sample());
        var r = await ex.ExtractAsync("x.png");
        Assert.Equal(2, r.Order.Items.Count);

        var first = r.Order.Items[0];
        Assert.Equal("MAT.OESX.02", first.Sku);
        Assert.Equal("Ergonomic Office Chair", first.Description);
        Assert.Equal(1, first.Quantity);
        Assert.Equal(570.00m, first.UnitNetPrice);
        Assert.Equal(19m, first.VatPercent);

        Assert.Equal(1235.00m, r.Order.ComputedNetTotal);
        Assert.Equal(234.65m, r.Order.ComputedVatTotal);
        Assert.Equal(1469.65m, r.Order.ComputedGrossTotal);
        Assert.Empty(r.Order.Items.SelectMany(_ => Array.Empty<string>()));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("mismatch"));
    }
}
