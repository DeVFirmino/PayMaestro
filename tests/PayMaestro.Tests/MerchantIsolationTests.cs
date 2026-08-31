using Microsoft.Extensions.Time.Testing;
using PayMaestro.Application.Fraud;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Fraud;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// Every read is scoped to the calling merchant. These tests hold that line for the reads that
/// do not name a payment directly, where a missing scope is silent rather than obvious.
/// </summary>
public class MerchantIsolationTests
{
    [Fact]
    public async Task Should_not_flag_the_card_when_the_declines_belong_to_another_merchant()
    {
        using var db = new PaymentDatabase();
        await SeedDeclines(db, merchantId: "merchant-b", count: 3);

        using PayMaestroDbContext context = db.NewContext();
        IFraudRule rule = new DeclineVelocityRule(new PaymentRepository(context), new FakeTimeProvider(DateTimeOffset.UtcNow));

        FraudVerdict verdict = await rule.EvaluateAsync(
            TestPayment.New(merchantId: "merchant-a"), CancellationToken.None);

        // A card declined at another merchant must neither reject this merchant's payment nor
        // let this merchant infer that the other merchant saw the card at all.
        Assert.False(verdict.IsSuspicious);
    }

    [Fact]
    public async Task Should_flag_the_card_when_the_declines_belong_to_the_same_merchant()
    {
        using var db = new PaymentDatabase();
        await SeedDeclines(db, merchantId: "merchant-a", count: 3);

        using PayMaestroDbContext context = db.NewContext();
        IFraudRule rule = new DeclineVelocityRule(new PaymentRepository(context), new FakeTimeProvider(DateTimeOffset.UtcNow));

        FraudVerdict verdict = await rule.EvaluateAsync(
            TestPayment.New(merchantId: "merchant-a"), CancellationToken.None);

        Assert.True(verdict.IsSuspicious);
    }

    private static async Task SeedDeclines(PaymentDatabase db, string merchantId, int count)
    {
        using PayMaestroDbContext context = db.NewContext();

        for (int index = 1; index <= count; index++)
        {
            Payment payment = TestPayment.New(merchantId: merchantId, idempotencyKey: $"decline-key-{index}");
            payment.BeginProcessing();

            PaymentAttempt attempt = PaymentAttempt.Start(payment.Id, "AlphaPay", 1, $"provider-key-{index}");
            attempt.Complete(GatewayResultType.HardDecline, "43", durationMs: 10);
            payment.RecordAttempt(attempt);
            payment.Decline();

            context.Payment.Add(payment);
        }

        await context.SaveChangesAsync();
    }
}
