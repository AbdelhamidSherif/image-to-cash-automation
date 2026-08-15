using ImageToCash.Core;

namespace ImageToCash.Flow;

public sealed class FlowOptions
{
    public bool DryRun { get; init; } = true;
    public string? ScreenshotDir { get; init; }
}

public sealed class ManualReviewException : Exception
{
    public ManualReviewException(string message) : base(message) { }
}

/// <summary>
/// Order-first orchestrator implementing the assessment's five stages with a
/// verify-before-advance checkpoint at every step. In dry-run mode, read-only and
/// navigational actions still execute but nothing is persisted (no Save / master
/// record creation / payment write).
/// </summary>
public sealed class ImageToCashFlow
{
    private readonly IOrderExtractor _extractor;
    private readonly IFakturamaAutomation _ui;
    private readonly FlowOptions _options;
    private readonly FlowResult _result = new();
    private OrderInfo _order = new();
    private int _shot;

    public ImageToCashFlow(IOrderExtractor extractor, IFakturamaAutomation ui, FlowOptions options)
    {
        _extractor = extractor;
        _ui = ui;
        _options = options;
    }

    public async Task<FlowResult> RunAsync(string imagePath, CancellationToken ct = default)
    {
        try
        {
            await Stage1Extract(imagePath, ct);
            Stage2OpenOrder();
            Stage3ResolveDebtor();
            Stage4ResolveProducts();
            Stage5SaveAndInvoice();
            _result.Completed = true;
        }
        catch (ManualReviewException ex)
        {
            _result.Error = ex.Message;
            _result.Add("manual review", "Flow stopped", StepStatus.ManualReview, ex.Message);
        }
        catch (Exception ex)
        {
            _result.Error = ex.Message;
            _result.Add("flow", "Unhandled error", StepStatus.Error, ex.Message);
        }
        return _result;
    }

    private async Task Stage1Extract(string imagePath, CancellationToken ct)
    {
        var extraction = await _extractor.ExtractAsync(imagePath, ct);
        _order = extraction.Order;
        foreach (var w in extraction.Warnings)
            _result.Add("1. extract", "Extraction warning", StepStatus.Warning, w);
        _result.Add("1. extract", "Extracted order", StepStatus.Ok,
            $"ref={_order.ExternalReference} date={_order.OrderDate:yyyy-MM-dd} items={_order.Items.Count} debtor={_order.Debtor.Company}");
        if (_order.Items.Count == 0)
            throw new ManualReviewException("No items were extracted from the image; cannot build an order.");
    }

    private void Stage2OpenOrder()
    {
        Shot("pre-open");
        _ui.OpenNewOrder();
        _result.Add("2. open", "Opened New Order", StepStatus.Ok);

        if (_order.OrderDate is { } d)
        {
            _ui.SetDate(d);
            _result.Add("2. open", "Set Date", StepStatus.Ok, d.ToString("yyyy-MM-dd"));
        }
        if (!string.IsNullOrWhiteSpace(_order.ExternalReference))
        {
            _ui.SetCustRef(_order.ExternalReference);
            _result.Add("2. open", "Set Cust.Ref.", StepStatus.Ok, _order.ExternalReference);
        }
        _ui.SetDocumentPriceMode("Net");
        _result.Add("2. open", "Set price mode Net / VAT With VAT", StepStatus.Ok);
        Shot("open-order");
    }

    private void Stage3ResolveDebtor()
    {
        var debtor = _order.Debtor;
        var query = debtor.Company ?? debtor.LastName ?? debtor.Alias;
        if (string.IsNullOrWhiteSpace(query))
        {
            _result.Add("3. debtor", "No debtor name to search", StepStatus.ManualReview, "Stop for manual review.");
            throw new ManualReviewException("No debtor name extracted; cannot resolve Debtor.");
        }

        var match = _ui.FindExistingDebtor(query);
        switch (match.Result)
        {
            case MatchResult.Exact:
                _ui.SelectDebtor(match.MatchedName!);
                _result.Add("3. debtor", "Selected existing Debtor", StepStatus.Ok, match.MatchedName);
                break;
            case MatchResult.Ambiguous:
                _result.Add("3. debtor", "Debtor search ambiguous", StepStatus.ManualReview, query);
                throw new ManualReviewException($"Debtor search was ambiguous for '{query}'; stop for manual review.");
            case MatchResult.None:
                if (_options.DryRun)
                {
                    _result.Add("3. debtor", "Create Debtor (skipped in dry-run)", StepStatus.Skipped, $"company={debtor.Company}");
                }
                else
                {
                    _ui.CreateDebtor(debtor);
                    _ui.SelectDebtor(debtor.Company ?? debtor.LastName ?? debtor.Alias!);
                    _result.Add("3. debtor", "Created new Debtor and selected it", StepStatus.Ok);
                }
                break;
        }
        Shot("debtor");
    }

    private void Stage4ResolveProducts()
    {
        foreach (var item in _order.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                _result.Add("4. product", "Item missing SKU", StepStatus.Warning, item.Description);
                continue;
            }
            var match = _ui.FindExistingProduct(item.Sku);
            switch (match.Result)
            {
                case MatchResult.Exact:
                    _ui.SelectProduct(match.MatchedSku!);
                    _result.Add("4. product", "Selected existing Product", StepStatus.Ok, item.Sku);
                    break;
                case MatchResult.Ambiguous:
                    _result.Add("4. product", "Product search ambiguous", StepStatus.ManualReview, item.Sku);
                    throw new ManualReviewException($"Product search ambiguous for '{item.Sku}'; stop for manual review.");
                case MatchResult.None:
                    if (_options.DryRun)
                    {
                        _result.Add("4. product", "Create Product (skipped in dry-run)", StepStatus.Skipped, $"{item.Sku}: {item.Description}");
                    }
                    else
                    {
                        _ui.CreateProduct(item);
                        _ui.SelectProduct(item.Sku);
                        _result.Add("4. product", "Created new Product and selected it", StepStatus.Ok, item.Sku);
                    }
                    break;
            }
            _ui.CompleteItemLine(item);
            _result.Add("4. product", "Completed item line", StepStatus.Ok, $"{item.Sku} qty={item.Quantity} unit={item.UnitNetPrice}");
        }
        Shot("products");
        VerifyTotals();
    }

    private void VerifyTotals()
    {
        var t = _ui.ReadTotals();
        var net = _order.ComputedNetTotal;
        var gross = _order.ComputedGrossTotal;
        var ok = (t.Gross is null || Math.Abs(t.Gross.Value - gross) <= 0.01m)
                 && (t.Net is null || Math.Abs(t.Net.Value - net) <= 0.01m);
        _result.Add("4. verify", "Verified totals", ok ? StepStatus.Ok : StepStatus.Warning,
            $"expected gross={gross:N2} net={net:N2}; UI gross={t.Gross?.ToString("N2") ?? "n/a"} net={t.Net?.ToString("N2") ?? "n/a"}");
        if (!ok)
            _result.Add("4. verify", "Totals mismatch", StepStatus.Warning, "UI totals differ from extracted/computed totals.");
    }

    private void Stage5SaveAndInvoice()
    {
        if (_options.DryRun)
        {
            _result.Add("5. save", "Save Order (skipped in dry-run)", StepStatus.Skipped);
            _result.Add("5. invoice", "Create Invoice (skipped in dry-run)", StepStatus.Skipped);
            _result.Add("5. payment", "Apply payment (skipped in dry-run)", StepStatus.Skipped);
            Shot("final");
            return;
        }

        _ui.SaveCurrentDocument();
        _result.Add("5. save", "Saved Order", StepStatus.Ok);
        Shot("order-saved");

        _ui.CreateInvoiceFromOrder();
        _result.Add("5. invoice", "Created linked Invoice from Order", StepStatus.Ok);

        if (_order.PaymentStatus == PaymentStatus.Paid)
        {
            _ui.SetPayment(PaymentStatus.Paid, _order.PaymentDate, _order.ComputedGrossTotal);
            _result.Add("5. payment", "Applied PAID status", StepStatus.Ok,
                $"date={_order.PaymentDate:yyyy-MM-dd} value={_order.ComputedGrossTotal:N2}");
        }
        else
        {
            _result.Add("5. payment", "Payment not PAID; left unpaid", StepStatus.Ok);
        }
        _ui.SaveCurrentDocument();
        _result.Add("5. payment", "Saved Invoice", StepStatus.Ok);
        Shot("invoice-final");

        var exists = _ui.DocumentExists("Order", _order.ExternalReference ?? "", _order.ComputedGrossTotal);
        _result.Add("5. verify", "Verified Order in Documents", exists ? StepStatus.Ok : StepStatus.Warning, exists ? "found" : "not found");
    }

    private void Shot(string label)
    {
        if (string.IsNullOrEmpty(_options.ScreenshotDir)) return;
        var file = Path.Combine(_options.ScreenshotDir, $"{++_shot:D2}-{label}.png");
        _ui.CaptureScreenshot(file);
    }
}
