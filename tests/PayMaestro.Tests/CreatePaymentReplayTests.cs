using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// What a reused idempotency key may replay and what it must refuse. A key only stands for the
/// exact request that reserved it: any change to what would be charged is a different payment
/// wearing an old key, and answering it with the stored outcome would report someone else's charge.
/// </summary>
public sealed class CreatePaymentReplayTests
{
    [Fact]
    public async Task ShouldRejectReuseWhenSameKeyCarriesDifferentCard()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext firstContext = db.NewContext();
        await db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().WithCardNumber("4111111111117777").Build(), CancellationToken.None);

        using PayMaestroDbContext secondContext = db.NewContext();
        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(
            () => db.NewCreatePaymentUseCase(secondContext, gateways: [gateway])
                    .Execute("key-1", new CreatePaymentRequestBuilder().WithCardNumber("5555444433331234").Build(), CancellationToken.None));

        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task ShouldRejectReuseWhenCardDiffersOnlyInMiddleDigits()
    {
        // Same BIN, same last four: only the fingerprint can tell these two cards apart.
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext firstContext = db.NewContext();
        await db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().WithCardNumber("4111111111117777").Build(), CancellationToken.None);

        using PayMaestroDbContext secondContext = db.NewContext();
        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(
            () => db.NewCreatePaymentUseCase(secondContext, gateways: [gateway])
                    .Execute("key-1", new CreatePaymentRequestBuilder().WithCardNumber("4111119999997777").Build(), CancellationToken.None));

        Assert.Equal(1, gateway.Charges);   // the second card never reached a provider
    }

    [Fact]
    public async Task ShouldStoreFingerprintAndNeverFullNumberWhenPaymentIsCreated()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext context = db.NewContext();
        await db.NewCreatePaymentUseCase(context, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().WithCardNumber("4111111111117777").Build(), CancellationToken.None);

        Payment stored = db.FindCommittedByKey("key-1")!;
        Assert.Equal(PaymentDatabase.CardFingerprinter.Fingerprint("4111111111117777"), stored.CardFingerprint);
        Assert.Equal("411111", stored.CardBin);
        Assert.Equal("7777", stored.CardLast4);
    }

    [Fact]
    public async Task ShouldReportSameUtcInstantWhenPaymentIsReplayed()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext firstContext = db.NewContext();
        PaymentResponse first = await db.NewCreatePaymentUseCase(firstContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext replayContext = db.NewContext();
        PaymentResponse replay = await db.NewCreatePaymentUseCase(replayContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        // Reloaded from SQLite, the timestamp must still say it is UTC — serialized without the
        // marker, "identical stored response" quietly becomes a different instant to the client.
        Assert.Equal(DateTimeKind.Utc, replay.CreatedAt.Kind);
        Assert.Equal(first.CreatedAt, replay.CreatedAt);
    }
}
