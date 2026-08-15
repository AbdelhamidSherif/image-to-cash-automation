namespace ImageToCash.Core;

public enum MatchResult
{
    Exact,
    Ambiguous,
    None
}

public sealed record DebtorMatch(MatchResult Result, string? MatchedName = null);
public sealed record ProductMatch(MatchResult Result, string? MatchedSku = null);
public sealed record OrderTotals(decimal? Net, decimal? Vat, decimal? Gross);

/// <summary>
/// Abstraction over the Fakturama UI so the flow logic can be unit-tested without
/// a live application instance.
/// </summary>
public interface IFakturamaAutomation
{
    void OpenNewOrder();
    void SetDate(DateTime date);
    void SetCustRef(string reference);
    void SetDocumentPriceMode(string mode);

    DebtorMatch FindExistingDebtor(string query);
    void CreateDebtor(DebtorInfo debtor);
    void SelectDebtor(string matchName);

    ProductMatch FindExistingProduct(string sku);
    void CreateProduct(ItemInfo item);
    void SelectProduct(string sku);
    void CompleteItemLine(ItemInfo item);

    OrderTotals ReadTotals();
    void SaveCurrentDocument();

    void CreateInvoiceFromOrder();
    void SetPayment(PaymentStatus status, DateTime? date, decimal value);

    bool DocumentExists(string kind, string reference, decimal total);
    void CaptureScreenshot(string label);
}
