using Microsoft.EntityFrameworkCore;
using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The migration adds the fingerprint column to rows that predate it, backfilled empty. Those
/// rows must keep replaying, and no caller-supplied value may be long enough to strand a
/// payment between the reservation and its first attempt.
/// </summary>
public class IdempotencyCompatibilityTests
{
    [Fact]
    public async Task Should_replay_the_stored_outcome_when_the_row_predates_the_fingerprint_column()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");
        Guid legacyId = await SeedRowWithoutFingerprint(db, "legacy-key");

        using PayMaestroDbContext context = db.NewContext();
        PaymentResponse replayed = await db.NewOrchestrator(context, gateways: [gateway])
            .Execute("merchant-1", "legacy-key", PaymentDatabase.Request());

        // An unknown fingerprint cannot be compared, so the stored outcome is returned rather
        // than the 422 that comparing against an empty backfill would produce.
        Assert.Equal(legacyId, replayed.Id);
        Assert.Equal("Captured", replayed.Status);
        Assert.Equal(0, gateway.Charges);
    }

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

    /// <summary>A settled payment as the migration leaves it: no fingerprint to compare against.</summary>
    private static async Task<Guid> SeedRowWithoutFingerprint(PaymentDatabase db, string idempotencyKey)
    {
        using PayMaestroDbContext context = db.NewContext();

        Payment payment = TestPayment.Reserved(idempotencyKey: idempotencyKey);
        PaymentAttempt attempt = PaymentAttempt.Start(payment.Id, "Alpha", 1, "legacy-provider-key");
        attempt.Complete(GatewayResultType.Approved, "00", durationMs: 5);
        payment.RecordAttempt(attempt);
        payment.Authorize();
        payment.Capture();

        context.Payment.Add(payment);
        await context.SaveChangesAsync();

        // The backfill the migration writes for pre-existing rows.
        await context.Payment
            .Where(candidate => candidate.Id == payment.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RequestFingerprint, string.Empty));

        return payment.Id;
    }
}
