using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.PaymentGateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The simulated provider's memory. A real acquirer does not forget a charge because the
/// orchestrator restarted, so neither may the mock: a fresh ledger reads what the old one wrote.
/// </summary>
public sealed class MockProviderLedgerTests
{
    private static readonly GatewayResult Approved = new(GatewayResultType.Approved, "00");
    private static readonly GatewayResult Declined = new(GatewayResultType.HardDecline, "43");

    [Fact]
    public async Task ShouldFindOutcomeWhenReadThroughFreshLedgerOverSameDatabase()
    {
        using PaymentDatabase db = new();
        await db.NewProviderLedger().SettleAsync("key-1:AlphaPay:1", Approved, CancellationToken.None);

        MockProviderLedger afterRestart = db.NewProviderLedger();
        GatewayResult? found = await afterRestart.FindAsync("key-1:AlphaPay:1", CancellationToken.None);

        Assert.Equal(Approved, found);
    }

    [Fact]
    public async Task ShouldKeepFirstOutcomeWhenSameKeyIsSettledTwice()
    {
        using PaymentDatabase db = new();
        MockProviderLedger ledger = db.NewProviderLedger();

        GatewayResult first = await ledger.SettleAsync("key-1:AlphaPay:1", Approved, CancellationToken.None);
        GatewayResult second = await db.NewProviderLedger().SettleAsync("key-1:AlphaPay:1", Declined, CancellationToken.None);

        Assert.Equal(Approved, first);
        Assert.Equal(Approved, second);     // the provider never rewrites an outcome it already produced
    }

    [Fact]
    public async Task ShouldReturnNullWhenKeyWasNeverSettled()
    {
        using PaymentDatabase db = new();

        GatewayResult? found = await db.NewProviderLedger().FindAsync("key-1:AlphaPay:1", CancellationToken.None);

        Assert.Null(found);
    }
}
