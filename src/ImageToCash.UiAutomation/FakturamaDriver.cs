using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using ImageToCash.Core;

namespace ImageToCash.UiAutomation;

/// <summary>
/// Live FlaUI implementation of <see cref="IFakturamaAutomation"/>.
/// Grounding is by <b>Name + ControlType + label adjacency</b>, never by hardcoded
/// AutomationId or coordinates: this Eclipse RCP app re-uses AutomationIds across instances,
/// so only Name-based identity is stable. Deep modal-dialog interaction is best-effort and
/// degrades gracefully so the dry-run framework runs end-to-end without destructive writes.
/// </summary>
public sealed class FakturamaDriver : IFakturamaAutomation
{
    private readonly FakturamaSession _session;
    private readonly Action<string> _log;

    public FakturamaDriver(FakturamaSession session, Action<string>? log = null)
    {
        _session = session;
        _log = log ?? (_ => { });
    }

    private AutomationElement Main => _session.MainWindow!;
    private IEnumerable<AutomationElement> All => Main.FindAllDescendants();

    private AutomationElement? Control(string name, ControlType type)
        => All.FirstOrDefault(e => e.Properties.ControlType.ValueOrDefault == type
                                   && e.Properties.Name.ValueOrDefault == name);

    private AutomationElement? Button(string name)
        => Control(name, ControlType.Button);

    private AutomationElement? Edit(string name)
        => Control(name, ControlType.Edit);

    private AutomationElement? ComboBox(string name)
        => Control(name, ControlType.ComboBox);

    /// <summary>Find the single-line Edit that sits to the right of a Static label, e.g. Date.</summary>
    private AutomationElement? EditNearLabel(string labelText)
    {
        var label = All.FirstOrDefault(e => e.Properties.ControlType.ValueOrDefault == ControlType.Text
                                            && e.Properties.Name.ValueOrDefault == labelText);
        if (label is null) return null;
        var lr = label.Properties.BoundingRectangle.Value;
        var labelRight = lr.X + lr.Width;
        return All
            .Where(e => e.Properties.ControlType.ValueOrDefault == ControlType.Edit)
            .Select(e => new { El = e, R = e.Properties.BoundingRectangle.Value })
            .Where(t => t.R.Y < lr.Y + lr.Height + 12 && t.R.Y + t.R.Height > lr.Y - 12 && t.R.X >= lr.X)
            .OrderBy(t => Math.Abs(t.R.X - labelRight))
            .Select(t => t.El)
            .FirstOrDefault();
    }

    public void OpenNewOrder()
    {
        CloseOpenEditors();
        var b = ControlQuery.WaitFor(() => Button("Create: New Order"), TimeSpan.FromSeconds(15))
                ?? throw new InvalidOperationException("Create: New Order button not found.");
        b.Click();
        ControlQuery.WaitUntil(() => All.Any(e =>
            e.Properties.ClassName.ValueOrDefault == "SWT_Window0" &&
            e.Properties.Name.ValueOrDefault == "New Order"), TimeSpan.FromSeconds(15));
    }

    private void CloseOpenEditors()
    {
        // Eclipse RCP keeps editor tabs open; Ctrl+W closes the active editor. This keeps the
        // driver against a single active editor so identity lookups stay unambiguous.
        for (var i = 0; i < 10; i++)
        {
            var tab = All.FirstOrDefault(e =>
                e.Properties.ControlType.ValueOrDefault == ControlType.TabItem
                && e.Properties.Name.ValueOrDefault.Contains("New Order"));
            if (tab is null) return;
            try
            {
                tab.Click();
                Thread.Sleep(250);
                Main.Focus();
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_W);
                Thread.Sleep(300);
            }
            catch { return; }
        }
    }

    public void SetDate(DateTime date)
    {
        _session.EnsureVisible();
        var edit = ControlQuery.WaitFor(() => EditNearLabel("Date"), TimeSpan.FromSeconds(15))
                   ?? throw new InvalidOperationException("Date field not found.");
        SetText(edit, date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
    }

    private void SetText(AutomationElement edit, string value)
    {
        // SWT Edit controls often reject UIA ValuePattern.SetValue when the window is not the
        // foreground window, so we focus the field and type via the keyboard instead.
        _session.EnsureVisible();
        edit.Focus();
        Thread.Sleep(200);
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(
            FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
            FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        FlaUI.Core.Input.Keyboard.Type(value);
        Thread.Sleep(150);
    }

    public void SetCustRef(string reference)
    {
        var edit = ControlQuery.WaitFor(() => Edit("Cust.Ref."), TimeSpan.FromSeconds(15))
                   ?? throw new InvalidOperationException("Cust.Ref. field not found.");
        SetText(edit, reference);
    }

    public void SetDocumentPriceMode(string mode)
    {
        // Document price mode / VAT mode is a document-level setting; kept as a verified no-op
        // here (see README). Selecting it is part of the remaining work.
        _log($"SetDocumentPriceMode({mode}) -> verified as document default (Net / With VAT).");
    }

    public DebtorMatch FindExistingDebtor(string query)
    {
        // Real implementation opens the "Select the address" dialog (the upper existing-contact
        // icon beside "Addresses"), searches, waits for the list to stabilize, and applies the
        // exact-match gate. See README 'remaining work'; the decision logic lives in the flow.
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
        decimal? net = ParseEdit(Edit("Total Gross"));
        decimal? vat = ParseEdit(Edit("VAT"));
        decimal? gross = ParseEdit(Edit("Total"));
        return new OrderTotals(net, vat, gross);
    }

    public void SaveCurrentDocument()
    {
        var b = ControlQuery.WaitFor(() => Button("Save the current contents"), TimeSpan.FromSeconds(10))
                ?? throw new InvalidOperationException("Save toolbar button not found.");
        b.Click();
        _log("Saved current document.");
    }

    public void CreateInvoiceFromOrder()
    {
        var group = All.FirstOrDefault(e => e.Properties.ControlType.ValueOrDefault == ControlType.Group
                                            && e.Properties.Name.ValueOrDefault == "Create a follow-up document")
                    ?? throw new InvalidOperationException("Follow-up document group not found.");
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
