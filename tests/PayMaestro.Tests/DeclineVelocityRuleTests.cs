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
/// The rule counts declines in the last 24 hours. A controlled clock lets a test move past
/// that window without waiting for it.
/// </summary>
public class DeclineVelocityRuleTests
{
    /// <summary>The clock starts where the seeded rows were written, then a test moves it.</summary>
    private static FakeTimeProvider ClockAtSeedTime() => new(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Should_flag_the_card_when_three_declines_fall_inside_the_window()
    {
        using var db = new PaymentDatabase();
        FakeTimeProvider clock = ClockAtSeedTime();
        await SeedDeclines(db, count: 3);

        using var context = db.NewContext();
        IFraudRule rule = new DeclineVelocityRule(new PaymentRepository(context), clock);

        FraudVerdict verdict = await rule.EvaluateAsync(TestPayment.New(), CancellationToken.None);

        Assert.True(verdict.IsSuspicious);
    }

    [Fact]
    public async Task Should_not_flag_the_card_when_two_declines_fall_inside_the_window()
    {
        using var db = new PaymentDatabase();
        FakeTimeProvider clock = ClockAtSeedTime();
        await SeedDeclines(db, count: 2);

        using var context = db.NewContext();
        IFraudRule rule = new DeclineVelocityRule(new PaymentRepository(context), clock);

        FraudVerdict verdict = await rule.EvaluateAsync(TestPayment.New(), CancellationToken.None);

        Assert.False(verdict.IsSuspicious);
    }

    [Fact]
    public async Task Should_not_flag_the_card_when_the_declines_are_older_than_the_window()
    {
        using var db = new PaymentDatabase();
        FakeTimeProvider clock = ClockAtSeedTime();
        await SeedDeclines(db, count: 3);

        clock.Advance(TimeSpan.FromHours(25)); // the declines have left the 24-hour window

        using var context = db.NewContext();
        IFraudRule rule = new DeclineVelocityRule(new PaymentRepository(context), clock);

        FraudVerdict verdict = await rule.EvaluateAsync(TestPayment.New(), CancellationToken.None);

        Assert.False(verdict.IsSuspicious);
    }

    /// <summary>Writes declined attempts on one card, which is what the rule counts.</summary>
    private static async Task SeedDeclines(PaymentDatabase db, int count)
    {
        using PayMaestroDbContext context = db.NewContext();

        for (int index = 1; index <= count; index++)
        {
            Payment payment = TestPayment.New(idempotencyKey: $"decline-key-{index}");
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
