using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentGateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The point of these tests is the one guarantee a payment orchestrator has to earn: two requests
/// carrying the same idempotency key must never produce two charges. They run against a real
/// SQLite file so the unique index does the deciding.
/// </summary>
public sealed class IdempotencyReservationTests
{
    [Fact]
    public async Task ShouldHaveCommittedReservationWhenGatewayIsCharging()
    {
        using PaymentDatabase db = new();
        Payment? visibleDuringCharge = null;

        TestGateway gateway = new("Alpha", whileCharging: () =>
        {
            visibleDuringCharge = db.FindCommittedByKey("key-1");
            return Task.CompletedTask;
        });

        using PayMaestroDbContext context = db.NewContext();
        await db.NewCreatePaymentUseCase(context, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        Assert.NotNull(visibleDuringCharge);
        Assert.Equal(PaymentStatus.Processing, visibleDuringCharge.Status);
    }

    [Fact]
    public async Task ShouldRefuseDuplicateWhenFirstRequestIsMidCharge()
    {
        using PaymentDatabase db = new();
        TaskCompletionSource reachedGateway = new();
        TaskCompletionSource releaseGateway = new();

        TestGateway gateway = new("Alpha", whileCharging: async () =>
        {
            reachedGateway.TrySetResult();
            await releaseGateway.Task;
        });

        using PayMaestroDbContext firstContext = db.NewContext();
        Task<PaymentResponse> firstRequest = db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        await reachedGateway.Task;              // the first request holds the key and is charging

        using PayMaestroDbContext secondContext = db.NewContext();
        await Assert.ThrowsAsync<PaymentInProgressException>(
            () => db.NewCreatePaymentUseCase(secondContext, gateways: [gateway])
                    .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None));

        releaseGateway.SetResult();
        PaymentResponse firstResponse = await firstRequest;

        Assert.Equal(nameof(PaymentStatus.Captured), firstResponse.Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task ShouldChargeOnceWhenTwoRequestsBothFindKeyFree()
    {
        // The real race: the loser looked the key up before the winner inserted it, so it is
        // working from a "this key is free" answer that is already stale.
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext loserContext = db.NewContext();
        TaskCompletionSource loserHasRead = new();
        TaskCompletionSource releaseLoser = new();
        GatedReadRepository gatedReader = new(new PaymentRepository(loserContext), loserHasRead, releaseLoser);

        Task<PaymentResponse> loserRequest = db.NewCreatePaymentUseCase(loserContext, reader: gatedReader, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        await loserHasRead.Task;

        using PayMaestroDbContext winnerContext = db.NewContext();
        PaymentResponse winner = await db.NewCreatePaymentUseCase(winnerContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        releaseLoser.SetResult();
        PaymentResponse loser = await loserRequest;

        Assert.Equal(1, gateway.Charges);           // the loser never reached a gateway
        Assert.Equal(winner.Id, loser.Id);          // it replays the winner's payment
        Assert.Equal(nameof(PaymentStatus.Captured), loser.Status);
        Assert.Equal(1, await db.CountPaymentsWithKeyAsync("key-1"));
    }

    [Fact]
    public async Task ShouldReplayOutcomeWhenKeyIsAlreadySettled()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext firstContext = db.NewContext();
        PaymentResponse first = await db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext replayContext = db.NewContext();
        PaymentResponse replay = await db.NewCreatePaymentUseCase(replayContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task ShouldRejectReuseWhenSameKeyCarriesDifferentAmount()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext firstContext = db.NewContext();
        await db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().WithAmount(100m).Build(), CancellationToken.None);

        using PayMaestroDbContext secondContext = db.NewContext();
        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(
            () => db.NewCreatePaymentUseCase(secondContext, gateways: [gateway])
                    .Execute("key-1", new CreatePaymentRequestBuilder().WithAmount(250m).Build(), CancellationToken.None));

        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task ShouldReplayProviderOutcomeWhenShippedMockGatewaySeesSettledKey()
    {
        // Guards the contract the mocks are meant to demonstrate, not just the test doubles.
        AlphaPayGateway gateway = new(new MockProviderLedger());
        Payment payment = new PaymentBuilder().Build();

        GatewayResult first = await gateway.ProcessAsync(payment, "provider-key-1", CancellationToken.None);
        GatewayResult second = await gateway.ProcessAsync(payment, "provider-key-1", CancellationToken.None);
        GatewayResult queried = await gateway.QueryAsync("provider-key-1", CancellationToken.None);

        Assert.Equal(GatewayResultType.Approved, first.ResultType);
        Assert.Same(first, second);          // the provider replays its own outcome
        Assert.Same(first, queried);
    }
}
