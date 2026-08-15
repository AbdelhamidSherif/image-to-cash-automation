using ImageToCash.Core;

namespace ImageToCash.Tests;

public class ArithmeticTests
{
    [Theory]
    [InlineData(1, 570.00, 19, 0, 570.00)]       // 570*1*(1-0)
    [InlineData(2, 350.00, 19, 5, 665.00)]       // 2*350*(0.95)
    [InlineData(3, 100.00, 7, 10, 270.00)]       // 3*100*0.9
    public void LineNet_Rounds(decimal qty, decimal unit, decimal vat, decimal disc, decimal expected)
    {
        var item = new ItemInfo
        {
            Quantity = qty,
            UnitNetPrice = unit,
            VatPercent = vat,
            DiscountPercent = disc
        };
        Assert.Equal(expected, item.LineNet);
    }

    [Fact]
    public void GrossTotal_AddsVat()
    {
        var o = new OrderInfo();
        o.Items.Add(new ItemInfo { Quantity = 1, UnitNetPrice = 570m, VatPercent = 19m });
        Assert.Equal(570m, o.ComputedNetTotal);
        Assert.Equal(108.30m, o.ComputedVatTotal);
        Assert.Equal(678.30m, o.ComputedGrossTotal);
    }
}
