using Lims.Domain.Inventory;

namespace Lims.UnitTests;

public class InventoryRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Fact]
    public void Earliest_expiry_is_used_first_and_batches_without_expiry_last()
    {
        var batches = new[] { new BatchStock(1, 10, Today.AddDays(300)), new BatchStock(2, 4, Today.AddDays(20)), new BatchStock(3, 10, null) };
        var (takes, short_) = InventoryRules.PlanConsumption(batches, 6, Today);
        Assert.Equal(0, short_);
        Assert.Equal([new Take(2, 4), new Take(1, 2)], takes);
        var (takes2, _) = InventoryRules.PlanConsumption(batches, 20, Today);
        Assert.Equal([2L, 1L, 3L], takes2.Select(t => t.BatchId));
    }

    [Fact]
    public void Expired_and_empty_batches_are_never_used_and_what_is_missing_is_reported()
    {
        var batches = new[] { new BatchStock(1, 50, Today.AddDays(-1)), new BatchStock(2, 0, Today.AddDays(10)), new BatchStock(3, 2, Today) };
        var (takes, shortfall) = InventoryRules.PlanConsumption(batches, 5, Today);
        Assert.Equal([new Take(3, 2)], takes);   // a batch that expires today is still usable
        Assert.Equal(3m, shortfall);
        Assert.Equal(2m, InventoryRules.PlanConsumption([], 2, Today).Shortfall);
    }

    [Fact]
    public void Quantities_are_rounded_to_three_decimals_and_ties_use_the_lower_batch_id()
    {
        var (takes, shortfall) = InventoryRules.PlanConsumption([new BatchStock(9, 1, Today.AddDays(5)), new BatchStock(4, 1, Today.AddDays(5))], 1.0004m, Today);
        Assert.Equal(0, shortfall);
        Assert.Equal([new Take(4, 1)], takes);
        Assert.Equal(1.235m, InventoryRules.Round(1.2345m));
    }

    [Theory]
    [InlineData(-1, 30, ExpiryState.Expired)]
    [InlineData(0, 30, ExpiryState.Expiring)]
    [InlineData(30, 30, ExpiryState.Expiring)]
    [InlineData(31, 30, ExpiryState.Ok)]
    public void Expiry_state_follows_the_alert_window(int daysFromToday, int window, ExpiryState expected) =>
        Assert.Equal(expected, InventoryRules.ExpiryOf(Today.AddDays(daysFromToday), Today, window));

    [Fact]
    public void An_item_without_expiry_is_never_expiring() => Assert.Equal(ExpiryState.Ok, InventoryRules.ExpiryOf(null, Today, 30));

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(4.999, 5, true)]
    [InlineData(5.001, 5, false)]
    [InlineData(0, 0, false)]   // no reorder level set: never "low"
    public void Low_stock_is_at_or_below_the_reorder_level(double onHand, double level, bool low) => Assert.Equal(low, InventoryRules.IsLow((decimal)onHand, (decimal)level));

    [Theory]
    [InlineData(MovementType.PurchaseIn, 5, 5)]
    [InlineData(MovementType.AdjustIn, -5, 5)]
    [InlineData(MovementType.TransferIn, 5, 5)]
    [InlineData(MovementType.Issue, 5, -5)]
    [InlineData(MovementType.AdjustOut, 5, -5)]
    [InlineData(MovementType.WriteOff, -5, -5)]
    [InlineData(MovementType.TransferOut, 5, -5)]
    [InlineData(MovementType.Consumption, 5, -5)]
    public void Direction_always_follows_the_movement_type(MovementType type, double quantity, double expected) =>
        Assert.Equal((decimal)expected, InventoryRules.Signed(type, (decimal)quantity));
}
