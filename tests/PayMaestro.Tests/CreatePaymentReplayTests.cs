using PayMaestro.Domain.Exceptions;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// What a reused idempotency key may replay and what it must refuse. A key only stands for the
/// exact request that reserved it: any change to what would be charged is a different payment
/// wearing an old key, and answering it with the stored outcome would report someone else's charge.
/// </summary>
public class CreatePaymentReplayTests
{
    [Fact]
    public async Task The_same_key_with_a_different_card_is_rejected()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var firstContext = db.NewContext();
        await db.NewOrchestrator(firstContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request(cardNumber: "4111111111117777"));

        using var secondContext = db.NewContext();
        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(
            () => db.NewOrchestrator(secondContext, gateways: [gateway])
                    .Execute("key-1", PaymentDatabase.Request(cardNumber: "5555444433331234")));

        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task A_replayed_payment_reports_the_same_utc_instant_as_the_original()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var firstContext = db.NewContext();
        var first = await db.NewOrchestrator(firstContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        using var replayContext = db.NewContext();
        var replay = await db.NewOrchestrator(replayContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        // Reloaded from SQLite, the timestamp must still say it is UTC — serialized without the
        // marker, "identical stored response" quietly becomes a different instant to the client.
        Assert.Equal(DateTimeKind.Utc, replay.CreatedAt.Kind);
        Assert.Equal(first.CreatedAt, replay.CreatedAt);
    }
}
