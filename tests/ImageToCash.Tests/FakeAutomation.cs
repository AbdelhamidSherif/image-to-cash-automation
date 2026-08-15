using ImageToCash.Core;

namespace ImageToCash.Tests;

public sealed class FakeAutomation : IFakturamaAutomation
{
    public int OpenNewOrderCalls { get; private set; }
    public int SaveCalls { get; private set; }
    public int CreateDebtorCalls { get; private set; }
    public int CreateProductCalls { get; private set; }
    public int InvoiceCalls { get; private set; }
    public int PaymentCalls { get; private set; }
    public int ScreenshotCalls { get; private set; }
    public bool DebtorExact { get; set; } = true;
    public bool DebtorAmbiguous { get; set; }
    public bool ProductExact { get; set; } = true;
    public bool ProductAmbiguous { get; set; }
    public OrderTotals? Totals { get; set; }
    public DateTime? LastDate { get; private set; }
    public string? LastCustRef { get; private set; }

    public void OpenNewOrder() => OpenNewOrderCalls++;
    public void SetDate(DateTime date) => LastDate = date;
    public void SetCustRef(string reference) => LastCustRef = reference;
    public void SetDocumentPriceMode(string mode) { }

    public DebtorMatch FindExistingDebtor(string query)
    {
        if (DebtorAmbiguous) return new DebtorMatch(MatchResult.Ambiguous, query);
        return DebtorExact ? new DebtorMatch(MatchResult.Exact, query) : new DebtorMatch(MatchResult.None);
    }

    public void CreateDebtor(DebtorInfo debtor) => CreateDebtorCalls++;
    public void SelectDebtor(string matchName) { }

    public ProductMatch FindExistingProduct(string sku)
    {
        if (ProductAmbiguous) return new ProductMatch(MatchResult.Ambiguous, sku);
        return ProductExact ? new ProductMatch(MatchResult.Exact, sku) : new ProductMatch(MatchResult.None);
    }

    public void CreateProduct(ItemInfo item) => CreateProductCalls++;
    public void SelectProduct(string sku) { }
    public void CompleteItemLine(ItemInfo item) { }

    public OrderTotals ReadTotals() => Totals ?? new OrderTotals(1235.00m, 234.65m, 1469.65m);
    public void SaveCurrentDocument() => SaveCalls++;

    public void CreateInvoiceFromOrder() => InvoiceCalls++;
    public void SetPayment(PaymentStatus status, DateTime? date, decimal value) => PaymentCalls++;

    public bool DocumentExists(string kind, string reference, decimal total) => true;
    public void CaptureScreenshot(string label) => ScreenshotCalls++;
}
