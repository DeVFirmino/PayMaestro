using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The guard rails around the idempotency key: a changed request never replays a stored
/// outcome, and no caller-supplied value can strand a payment between the reservation and its
/// first attempt.
/// </summary>
public class IdempotencyGuardrailTests
{
    [Fact]
    public async Task Should_reject_the_replay_when_a_stored_fingerprint_does_not_match()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using PayMaestroDbContext first = db.NewContext();
        await db.NewOrchestrator(first, gateways: [gateway])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request(amount: 100m));

        using PayMaestroDbContext second = db.NewContext();
        await Assert.ThrowsAsync<Domain.Exceptions.IdempotencyKeyReuseException>(
            () => db.NewOrchestrator(second, gateways: [gateway])
                    .Execute("merchant-1", "key-1", PaymentDatabase.Request(amount: 250m)));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1)]
    public void Should_keep_the_provider_key_within_its_column_when_the_merchant_id_is_long(int merchantIdLength)
    {
        Payment payment = TestPayment.Reserved(idempotencyKey: new string('k', 100));
        typeof(Payment).GetProperty(nameof(Payment.MerchantId))!
            .SetValue(payment, new string('m', merchantIdLength));

        string providerKey = ProviderIdempotencyKey.For(payment, "GammaPay", 3);

        // The column holds 200 characters. Hashing the caller-supplied parts keeps the key a
        // constant size, so it can never throw after the reservation was already committed.
        Assert.True(providerKey.Length <= 200, $"provider key was {providerKey.Length} characters");
    }

    [Fact]
    public void Should_refuse_a_merchant_id_the_column_cannot_hold()
    {
        // Refused while building the payment, before anything is committed, so the caller gets
        // an error instead of a payment stranded in Processing with no attempt to recover.
        Assert.Throws<ArgumentException>(
            () => TestPayment.New(merchantId: new string('m', Payment.MaxMerchantIdLength + 1)));
    }

    [Fact]
    public void Should_derive_a_different_provider_key_for_each_merchant_using_the_same_client_key()
    {
        Payment first = TestPayment.Reserved();
        Payment second = TestPayment.New(merchantId: "merchant-2");

        Assert.NotEqual(
            ProviderIdempotencyKey.For(first, "AlphaPay", 1),
            ProviderIdempotencyKey.For(second, "AlphaPay", 1));
    }
}
