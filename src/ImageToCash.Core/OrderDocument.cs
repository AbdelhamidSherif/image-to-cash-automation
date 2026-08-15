namespace ImageToCash.Core;

public enum PaymentStatus
{
    Unspecified,
    Paid,
    Unpaid
}

public sealed class AddressInfo
{
    public string? Street { get; set; }
    public string? Zip { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public sealed class DebtorInfo
{
    public string? Company { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Alias { get; set; }
    public AddressInfo? InvoiceAddress { get; set; }
    public AddressInfo? DeliveryAddress { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentMethodCode { get; set; }
}

public sealed class ItemInfo
{
    public string? Sku { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal VatPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal? SourceTotal { get; set; }

    public decimal LineNet =>
        decimal.Round(Quantity * UnitNetPrice * (1 - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}

public sealed class OrderInfo
{
    public DateTime? OrderDate { get; set; }
    public string? ExternalReference { get; set; }
    public DebtorInfo Debtor { get; set; } = new();
    public List<ItemInfo> Items { get; set; } = new();

    public string DocumentPriceMode { get; set; } = "Net";
    public string VatMode { get; set; } = "WithVAT";

    public PaymentStatus PaymentStatus { get; set; }
    public DateTime? PaymentDate { get; set; }

    public decimal? SourceTotalNet { get; set; }
    public decimal? SourceVat { get; set; }
    public decimal? SourceTotal { get; set; }

    public decimal ComputedNetTotal => decimal.Round(Items.Sum(i => i.LineNet), 2, MidpointRounding.AwayFromZero);

    public decimal ComputedVatTotal =>
        decimal.Round(Items.Sum(i => decimal.Round(i.LineNet * i.VatPercent / 100m, 2, MidpointRounding.AwayFromZero)), 2, MidpointRounding.AwayFromZero);

    public decimal ComputedGrossTotal => decimal.Round(ComputedNetTotal + ComputedVatTotal, 2, MidpointRounding.AwayFromZero);
}
