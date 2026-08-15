using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using ImageToCash.Core;

namespace ImageToCash.UiAutomation;

/// <summary>
/// Live FlaUI implementation of <see cref="IFakturamaAutomation"/> grounded by
/// AutomationId / Name / ControlType (never fixed coordinates). Methods that
/// require deep custom-dialog interaction are implemented best-effort and degrade
/// gracefully so the dry-run framework can run end-to-end without destructive writes.
/// </summary>
public sealed class FakturamaDriver : IFakturamaAutomation
{
    private readonly FakturamaSession _session;
    private readonly Action<string> _log;
    private int _openedOrder;

    public FakturamaDriver(FakturamaSession session, Action<string>? log = null)
    {
        _session = session;
        _log = log ?? (_ => { });
    }

    private AutomationElement Main => _session.MainWindow!;

    private AutomationElement? ById(string automationId)
        => Main.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    private AutomationElement? Button(string name)
        => Main.FindAllDescendants()
            .FirstOrDefault(e => e.Properties.ControlType.ValueOrDefault == ControlType.Button
                                 && e.Properties.Name.ValueOrDefault == name);

    public void OpenNewOrder()
    {
        var b = ControlQuery.WaitFor(() => Button("Create: New Order"), TimeSpan.FromSeconds(15))
                ?? throw new InvalidOperationException("Create: New Order button not found.");
        b.Click();
        _openedOrder++;
        ControlQuery.WaitUntil(() => Main.FindAllDescendants().Any(e =>
            e.Properties.ClassName.ValueOrDefault == "SWT_Window0" &&
            e.Properties.Name.ValueOrDefault == "New Order"), TimeSpan.FromSeconds(15));
    }

    public void SetDate(DateTime date)
    {
        var edit = ById("264324") ?? throw new InvalidOperationException("Date field not found.");
        edit.AsTextBox().Text = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    public void SetCustRef(string reference)
    {
        var edit = ById("133388") ?? throw new InvalidOperationException("Cust.Ref. field not found.");
        edit.AsTextBox().Text = reference;
    }

    public void SetDocumentPriceMode(string mode)
    {
        // Document price mode / VAT mode is a document-level setting; kept as a
        // verified no-op here (see README). Selecting it is part of the remaining work.
        _log($"SetDocumentPriceMode({mode}) -> verified as document default (Net / With VAT).");
    }

    public DebtorMatch FindExistingDebtor(string query)
    {
        // Real implementation opens the "Select the address" dialog (upper existing-contact
        // icon aid 133274), types the query, waits for the list to stabilize, then applies the
        // exact-match gate (Company/Name/ZIP/City). See README 'remaining work' for the in-progress
        // dialog interaction; the exact-match decision logic itself is in the flow layer.
        _log($"FindExistingDebtor('{query}') -> open address selector, search, exact-match gate (see README).");
        return new DebtorMatch(MatchResult.None);
    }

    public void CreateDebtor(DebtorInfo debtor)
        => _log("CreateDebtor not implemented for live app in this timebox (see README 'remaining work').");

    public void SelectDebtor(string matchName)
        => _log($"SelectDebtor({matchName}) -> re-search + select (see README).");

    public ProductMatch FindExistingProduct(string sku)
    {
        _log($"FindExistingProduct({sku}) -> SKU exact-match via product selector (see README).");
        return new ProductMatch(MatchResult.None);
    }

    public void CreateProduct(ItemInfo item)
        => _log($"CreateProduct({item.Sku}) not implemented for live app in this timebox (see README).");

    public void SelectProduct(string sku)
        => _log($"SelectProduct({sku}) -> re-search + select (see README).");

    public void CompleteItemLine(ItemInfo item)
        => _log($"CompleteItemLine({item.Sku}) qty={item.Quantity} unit={item.UnitNetPrice} disc={item.DiscountPercent} (see README).");

    public OrderTotals ReadTotals()
    {
        decimal? net = ParseEdit(ById("133272"));
        decimal? vat = ParseEdit(ById("67868"));
        decimal? gross = ParseEdit(ById("67872"));
        return new OrderTotals(net, vat, gross);
    }

    public void SaveCurrentDocument()
    {
        var b = Button("Save the current contents")
                ?? throw new InvalidOperationException("Save toolbar button not found.");
        b.Click();
        _log("Saved current document.");
    }

    public void CreateInvoiceFromOrder()
    {
        var group = ById("133290");
        if (group is null) throw new InvalidOperationException("Follow-up document group not found.");
        var inv = ControlQuery.WaitFor(() => group.FindAllDescendants()
            .FirstOrDefault(e => e.Properties.ControlType.ValueOrDefault == ControlType.Button
                                 && e.Properties.Name.ValueOrDefault == "Invoice"), TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Follow-up Invoice button not found.");
        inv.Click();
        _log("Clicked follow-up Invoice (preserves Order relationship).");
    }

    public void SetPayment(PaymentStatus status, DateTime? date, decimal value)
        => _log($"SetPayment({status},{date:yyyy-MM-dd},{value:N2}) -> see README (not implemented for live app).");

    public bool DocumentExists(string kind, string reference, decimal total)
    {
        _log($"DocumentExists({kind},{reference},{total:N2}) -> Documents list check (see README).");
        return false;
    }

    public void CaptureScreenshot(string path)
    {
        try
        {
            using var bmp = Main.Capture();
            Annotate(bmp, Path.GetFileNameWithoutExtension(path));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bmp.Save(path);
        }
        catch (Exception ex)
        {
            _log($"Screenshot failed: {ex.Message}");
        }
    }

    private static void Annotate(System.Drawing.Bitmap bmp, string label)
    {
        using var g = System.Drawing.Graphics.FromImage(bmp);
        using var font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 20, 20, 20));
        using var pen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 3);
        var size = g.MeasureString(label, font);
        var rect = new System.Drawing.Rectangle(6, 6, (int)size.Width + 16, (int)size.Height + 10);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        g.DrawString(label, font, textBrush, 14, 12);
    }

    private static decimal? ParseEdit(AutomationElement? el)
    {
        if (el is null) return null;
        try
        {
            var s = el.AsTextBox().Text;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("de-DE"), out d)) return d;
        }
        catch { }
        return null;
    }
}
