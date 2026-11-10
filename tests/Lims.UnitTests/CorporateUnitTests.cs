using Lims.Domain.Billing;

namespace Lims.UnitTests;

public sealed class CorporateRulesTests
{
    private static readonly DateOnly Due = new(2026, 11, 1);

    [Theory]
    [InlineData(0, AgingBucket.NotDue)]     // due today is not yet late
    [InlineData(-5, AgingBucket.NotDue)]
    [InlineData(1, AgingBucket.Days1To30)]
    [InlineData(30, AgingBucket.Days1To30)]
    [InlineData(31, AgingBucket.Days31To60)]
    [InlineData(60, AgingBucket.Days31To60)]
    [InlineData(61, AgingBucket.Days61To90)]
    [InlineData(90, AgingBucket.Days61To90)]
    [InlineData(91, AgingBucket.Over90)]
    [InlineData(400, AgingBucket.Over90)]
    public void Aging_buckets_follow_the_days_past_due(int daysLate, AgingBucket expected) =>
        Assert.Equal(expected, CorporateRules.BucketOf(Due, Due.AddDays(daysLate)));

    [Theory]
    [InlineData(1000, 400, 400)]      // limited by the invoice
    [InlineData(300, 400, 300)]       // limited by the receipt
    [InlineData(0, 400, 0)]
    [InlineData(500, 0, 0)]
    [InlineData(500, -50, 0)]         // a refund-due invoice can take nothing
    [InlineData(100.005, 400, 100.01)]
    public void An_allocation_is_never_more_than_the_receipt_has_left_or_the_invoice_owes(double receiptLeft, double invoiceBalance, double expected) =>
        Assert.Equal((decimal)expected, CorporateRules.MaxAllocation((decimal)receiptLeft, (decimal)invoiceBalance));

    [Fact]
    public void The_account_balance_is_the_same_however_receipts_are_allocated()
    {
        // Invoiced 3,000; received 1,200. Whether the receipt is matched to an invoice or not, the client owes 1,800.
        Assert.Equal(1800m, CorporateRules.AccountBalance(outstandingInvoices: 3000m, unallocatedReceipts: 1200m));   // nothing allocated yet
        Assert.Equal(1800m, CorporateRules.AccountBalance(outstandingInvoices: 1800m, unallocatedReceipts: 0m));      // fully allocated
        Assert.Equal(1800m, CorporateRules.AccountBalance(outstandingInvoices: 2400m, unallocatedReceipts: 600m));    // half allocated
        Assert.Equal(-200m, CorporateRules.AccountBalance(0m, 200m));                                                  // overpaid: the client is in credit
    }

    [Theory]
    [InlineData(null, 9999999.0, 1.0, false)]       // no limit
    [InlineData(1000.0, 500.0, 500.0, false)]       // exactly at the limit is allowed
    [InlineData(1000.0, 500.0, 500.01, true)]
    [InlineData(1000.0, -200.0, 1200.0, false)]     // credit in hand makes room
    [InlineData(0.0, 0.0, 1.0, true)]               // a zero limit means no credit at all
    public void The_credit_limit_blocks_only_what_goes_past_it(double? limit, double balance, double invoice, bool exceeds) =>
        Assert.Equal(exceeds, CorporateRules.ExceedsLimit((decimal?)limit, (decimal)balance, (decimal)invoice));

    [Fact]
    public void A_contract_covers_its_dates_only_while_active()
    {
        var c = new CorporateContract { ValidFrom = new DateOnly(2026, 1, 1), ValidTo = new DateOnly(2026, 12, 31), IsActive = true };
        Assert.True(c.Covers(new DateOnly(2026, 1, 1)));
        Assert.True(c.Covers(new DateOnly(2026, 12, 31)));
        Assert.False(c.Covers(new DateOnly(2025, 12, 31)));
        Assert.False(c.Covers(new DateOnly(2027, 1, 1)));
        c.IsActive = false;
        Assert.False(c.Covers(new DateOnly(2026, 6, 1)));
        Assert.True(new CorporateContract { ValidFrom = new DateOnly(2026, 1, 1), ValidTo = null, IsActive = true }.Covers(new DateOnly(2099, 1, 1)));   // open-ended
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_like_the_rest_of_billing()
    {
        Assert.Equal(0.13m, CorporateRules.Round(0.125m));
        Assert.Equal(-0.13m, CorporateRules.Round(-0.125m));
        Assert.Equal(10m, CorporateRules.Round(9.999m));
    }
}
