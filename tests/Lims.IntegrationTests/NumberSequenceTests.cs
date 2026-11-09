using Lims.Domain.Core;
using Lims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class NumberSequenceTests(DatabaseFixture fx)
{
    private async Task<string> Next(string type, Func<Lims.Application.Abstractions.RequestContext>? who = null)
    {
        await using var db = fx.Create((who ?? (() => fx.UserOfA()))());
        return await new NumberSequenceService(db, new LabClock(TimeProvider.System)).NextAsync(type);
    }

    private static long Tail(string number) => long.Parse(number[(number.LastIndexOf('-') + 1)..]);

    [Fact]
    public async Task Parallel_requests_never_receive_the_same_number()
    {
        var first = Tail(await Next(SequenceTypes.Receipt));
        var numbers = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => Next(SequenceTypes.Receipt))));

        Assert.Equal(40, numbers.Distinct().Count());
        var tails = numbers.Select(Tail).OrderBy(x => x).ToArray();
        Assert.Equal(Enumerable.Range(1, 40).Select(i => first + i), tails); // consecutive, no gaps
    }

    [Fact]
    public async Task Rolled_back_transaction_returns_its_number()
    {
        await using var db = fx.Create(fx.UserOfA());
        var service = new NumberSequenceService(db, new LabClock(TimeProvider.System));

        string discarded;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            discarded = await service.NextAsync(SequenceTypes.Invoice);
            await tx.RollbackAsync();
        }

        Assert.Equal(discarded, await service.NextAsync(SequenceTypes.Invoice));
    }

    [Fact]
    public async Task Organisations_have_independent_sequences()
    {
        var a = await Next(SequenceTypes.PurchaseReceipt);
        var b1 = await Next(SequenceTypes.PurchaseReceipt, () => fx.UserOfB());
        var b2 = await Next(SequenceTypes.PurchaseReceipt, () => fx.UserOfB());
        Assert.StartsWith("GRN-", a);
        Assert.Equal(Tail(b1) + 1, Tail(b2));
    }

    [Fact]
    public async Task Monthly_sequence_restarts_in_a_new_period()
    {
        await using (var sys = fx.System())
            await sys.Database.ExecuteSqlRawAsync(
                "UPDATE core.NumberSequences SET CurrentPeriod = '0001', NextValue = 977 WHERE OrganizationId = {0} AND SequenceType = 'Order'", fx.OrgA);

        var number = await Next(SequenceTypes.Order);
        var period = NumberSequence.PeriodFor(SequenceResetPolicy.Monthly, new LabClock(TimeProvider.System).LocalNow);
        Assert.Equal($"ORD-{period}-00001", number);
    }

    [Fact]
    public async Task Branch_specific_sequence_takes_priority()
    {
        await using (var db = fx.Create(fx.UserOfA()))
        {
            db.NumberSequences.Add(new NumberSequence
            {
                BranchId = fx.OrgASecondBranch, SequenceType = SequenceTypes.Sample, Prefix = "CC1", Padding = 3,
                ResetPolicy = SequenceResetPolicy.Never,
            });
            await db.SaveChangesAsync();
        }

        await using var ctx = fx.Create(fx.UserOfA());
        var service = new NumberSequenceService(ctx, new LabClock(TimeProvider.System));
        Assert.Equal("CC1-001", await service.NextAsync(SequenceTypes.Sample, fx.OrgASecondBranch));
        Assert.StartsWith("S-", await service.NextAsync(SequenceTypes.Sample, fx.OrgAMainBranch));
    }
}
