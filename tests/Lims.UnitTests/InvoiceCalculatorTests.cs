using Lims.Domain.Billing;

namespace Lims.UnitTests;

public sealed class InvoiceCalculatorTests
{
    private static Invoice With(params decimal[] prices)
    {
        var inv = new Invoice();
        foreach (var p in prices) inv.Lines.Add(new InvoiceLine { Price = p });
        return inv;
    }

    [Fact]
    public void Unpaid_invoice_owes_the_net()
    {
        var inv = With(1200, 400);
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((1600m, 0m, 1600m, 1600m, InvoiceStatus.Unpaid), (inv.Gross, inv.DiscountAmount, inv.Net, inv.Balance, inv.Status));
    }

    [Fact]
    public void Percentage_discount_is_reapplied_when_a_line_is_cancelled()
    {
        var inv = With(1000, 500);
        inv.DiscountPercent = 10;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal(150m, inv.DiscountAmount);

        inv.Lines.Last().IsCancelled = true;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((1000m, 100m, 900m), (inv.Gross, inv.DiscountAmount, inv.Net));
    }

    [Fact]
    public void Fixed_discount_never_exceeds_the_remaining_gross()
    {
        var inv = With(1000, 300);
        inv.DiscountAmount = 500;
        InvoiceCalculator.Recalculate(inv);
        inv.Lines.First().IsCancelled = true;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((300m, 300m, 0m), (inv.Gross, inv.DiscountAmount, inv.Net));
    }

    [Fact]
    public void Partial_full_and_over_payment_statuses()
    {
        var inv = With(1000);
        inv.Payments.Add(new Payment { Amount = 400 });
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((InvoiceStatus.PartiallyPaid, 600m), (inv.Status, inv.Balance));

        inv.Payments.Add(new Payment { Amount = 600 });
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((InvoiceStatus.Paid, 0m), (inv.Status, inv.Balance));

        // Test cancelled after full payment: money is owed back.
        inv.Lines.First().IsCancelled = true;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((InvoiceStatus.RefundDue, -1000m), (inv.Status, inv.Balance));

        inv.Refunds.Add(new Refund { Amount = 1000 });
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((InvoiceStatus.Cancelled, 0m), (inv.Status, inv.Balance));
    }

    [Fact]
    public void Money_is_rounded_half_away_from_zero()
    {
        var inv = With(333.33m, 333.33m, 333.34m);
        inv.DiscountPercent = 12.5m;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal(125.00m, inv.DiscountAmount);
        Assert.Equal(0.13m, InvoiceCalculator.Round(0.125m));
        Assert.Equal(-0.13m, InvoiceCalculator.Round(-0.125m));
    }

    [Fact]
    public void Fully_discounted_invoice_is_paid()
    {
        var inv = With(500);
        inv.DiscountPercent = 100;
        InvoiceCalculator.Recalculate(inv);
        Assert.Equal((0m, InvoiceStatus.Paid), (inv.Net, inv.Status));
    }
}
