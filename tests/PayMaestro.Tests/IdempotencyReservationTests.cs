using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentGateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The point of these tests is the one guarantee a payment orchestrator has to earn: two requests
/// carrying the same idempotency key must never produce two charges. They run against a real
/// SQLite file so the unique index does the deciding.
/// </summary>
public class IdempotencyReservationTests
{
    [Fact]
    public async Task The_key_is_reserved_in_the_database_before_the_gateway_is_called()
    {
        using var db = new PaymentDatabase();
        Payment? visibleDuringCharge = null;

        var gateway = new TestGateway("Alpha", whileCharging: () =>
        {
            // A separate connection: only committed state is visible here.
            using var observer = db.NewContext();
            visibleDuringCharge = observer.Payment.AsNoTracking()
                .FirstOrDefault(p => p.IdempotencyKey == "key-1");
            return Task.CompletedTask;
        });

        using var context = db.NewContext();
        await db.NewOrchestrator(context, gateways: [gateway]).Execute("key-1", PaymentDatabase.Request());

        Assert.NotNull(visibleDuringCharge);
        Assert.Equal(PaymentStatus.Processing, visibleDuringCharge!.Status);
    }

    [Fact]
    public async Task A_duplicate_arriving_mid_charge_is_refused_instead_of_charging_again()
    {
        using var db = new PaymentDatabase();
        var reachedGateway = new TaskCompletionSource();
        var releaseGateway = new TaskCompletionSource();

        var gateway = new TestGateway("Alpha", whileCharging: async () =>
        {
            reachedGateway.TrySetResult();
            await releaseGateway.Task;
        });

        using var firstContext = db.NewContext();
        var firstRequest = db.NewOrchestrator(firstContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        await reachedGateway.Task;              // the first request holds the key and is charging

        using var secondContext = db.NewContext();
        await Assert.ThrowsAsync<PaymentInProgressException>(
            () => db.NewOrchestrator(secondContext, gateways: [gateway])
                    .Execute("key-1", PaymentDatabase.Request()));

        releaseGateway.SetResult();
        var firstResponse = await firstRequest;

        Assert.Equal(nameof(PaymentStatus.Captured), firstResponse.Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task Two_requests_that_both_find_the_key_free_still_charge_once()
    {
        // The real race: the loser looked the key up before the winner inserted it, so it is
        // working from a "this key is free" answer that is already stale.
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var loserContext = db.NewContext();
        var loserHasRead = new TaskCompletionSource();
        var releaseLoser = new TaskCompletionSource();
        var gatedRepo = new GatedReadRepository(new PaymentRepository(loserContext), loserHasRead, releaseLoser);

        var loserRequest = db.NewOrchestrator(loserContext, readRepo: gatedRepo, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        await loserHasRead.Task;

        using var winnerContext = db.NewContext();
        var winner = await db.NewOrchestrator(winnerContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        releaseLoser.SetResult();
        var loser = await loserRequest;

        Assert.Equal(1, gateway.Charges);           // the loser never reached a gateway
        Assert.Equal(winner.Id, loser.Id);          // it replays the winner's payment
        Assert.Equal(nameof(PaymentStatus.Captured), loser.Status);

        using var verification = db.NewContext();
        Assert.Equal(1, await verification.Payment.CountAsync(p => p.IdempotencyKey == "key-1"));
    }

    [Fact]
    public async Task A_settled_key_replays_without_touching_a_gateway()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var firstContext = db.NewContext();
        var first = await db.NewOrchestrator(firstContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        using var replayContext = db.NewContext();
        var replay = await db.NewOrchestrator(replayContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task The_same_key_with_a_different_amount_is_rejected()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var firstContext = db.NewContext();
        await db.NewOrchestrator(firstContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request(amount: 100m));

        using var secondContext = db.NewContext();
        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(
            () => db.NewOrchestrator(secondContext, gateways: [gateway])
                    .Execute("key-1", PaymentDatabase.Request(amount: 250m)));

        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task The_shipped_mock_gateway_honours_a_key_it_has_already_settled()
    {
        // Guards the contract the mocks are meant to demonstrate, not just the test doubles.
        var gateway = new AlphaPayGateway(new MockProviderLedger());
        var payment = Payment.Create("key-1", "ORDER-1", "cust-1", 100m, "EUR",
            "411111", "7777", "MT", "203.0.113.10", "MT");

        var first = await gateway.ProcessAsync(payment, "provider-key-1");
        var second = await gateway.ProcessAsync(payment, "provider-key-1");
        var queried = await gateway.QueryAsync("provider-key-1");

        Assert.Equal(GatewayResultType.Approved, first.ResultType);
        Assert.Same(first, second);          // the provider replays its own outcome
        Assert.Same(first, queried);
    }
}
