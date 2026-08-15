using ImageToCash.Core;
using ImageToCash.Flow;

namespace ImageToCash.Tests;

public class FlowTests
{
    private static IOrderExtractor SampleExtractor() => new FakeExtractor();

    [Fact]
    public async Task DryRun_ExactMatches_CompletesAndSkipsWrites()
    {
        var ui = new FakeAutomation();
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = true });
        var result = await flow.RunAsync("x.png");

        Assert.True(result.Completed);
        Assert.Equal(0, ui.SaveCalls);
        Assert.Equal(0, ui.CreateDebtorCalls);
        Assert.Equal(0, ui.CreateProductCalls);
        Assert.Equal(0, ui.InvoiceCalls);
        Assert.Equal(0, ui.PaymentCalls);
        Assert.NotNull(ui.LastDate);
        Assert.Equal("WEB.2026-0714-A17", ui.LastCustRef);
        Assert.Contains(result.Steps, s => s.Status == StepStatus.Skipped && s.Action.Contains("skipped in dry-run"));
    }

    [Fact]
    public async Task Live_ExactMatches_ExecutesWrites()
    {
        var ui = new FakeAutomation();
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = false });
        var result = await flow.RunAsync("x.png");

        Assert.True(result.Completed);
        Assert.Equal(2, ui.SaveCalls);       // order + invoice
        Assert.Equal(1, ui.InvoiceCalls);
        Assert.Equal(1, ui.PaymentCalls);    // PAID
        Assert.Equal(0, ui.CreateDebtorCalls);
        Assert.Equal(0, ui.CreateProductCalls);
    }

    [Fact]
    public async Task AmbiguousDebtor_StopsForManualReview()
    {
        var ui = new FakeAutomation { DebtorAmbiguous = true };
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = false });
        var result = await flow.RunAsync("x.png");

        Assert.False(result.Completed);
        Assert.Contains("manual review", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Steps, s => s.Status == StepStatus.ManualReview);
    }

    [Fact]
    public async Task AmbiguousProduct_StopsForManualReview()
    {
        var ui = new FakeAutomation { ProductAmbiguous = true };
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = false });
        var result = await flow.RunAsync("x.png");

        Assert.False(result.Completed);
        Assert.Contains("manual review", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingDebtor_Live_CreatesAndSelects()
    {
        var ui = new FakeAutomation { DebtorExact = false };
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = false });
        var result = await flow.RunAsync("x.png");

        Assert.True(result.Completed);
        Assert.Equal(1, ui.CreateDebtorCalls);
    }

    [Fact]
    public async Task Screenshots_CapturedWhenDirConfigured()
    {
        var ui = new FakeAutomation();
        var flow = new ImageToCashFlow(SampleExtractor(), ui, new FlowOptions { DryRun = true, ScreenshotDir = "shots" });
        await flow.RunAsync("x.png");
        Assert.True(ui.ScreenshotCalls > 0);
    }
}
