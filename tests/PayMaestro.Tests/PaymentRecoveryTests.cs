using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// Recovery settles what a stopped process left behind. A recovered payment must always end in
/// a state something can act on: never left in Processing, where its idempotency key would
/// answer 409 forever and no cascade would ever pick it up again.
/// </summary>
public class PaymentRecoveryTests
{
    [Fact]
    public async Task Should_decline_the_payment_when_the_provider_reports_a_soft_decline()
    {
        using var db = new PaymentDatabase();
        var gateway = new StaticQueryGateway("Alpha", GatewayResultType.SoftDecline, "51");
        Guid paymentId = await SeedStaleProcessingPayment(db, "key-soft");

        using PayMaestroDbContext recoveryContext = db.NewContext();
        int recovered = await db.NewRecovery(recoveryContext, gateway).Execute(Cutoff, take: 10);

        using PayMaestroDbContext verification = db.NewContext();
        Payment payment = await verification.Payment.Include(p => p.Attempts).SingleAsync(p => p.Id == paymentId);

        // The live cascade would try the next acquirer, but recovery has no cascade left to
        // continue. Declining keeps the payment actionable instead of stuck in Processing.
        Assert.Equal(1, recovered);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Completed, payment.Attempts.Single().Status);
    }

    [Fact]
    public async Task Should_decline_the_payment_when_the_provider_holds_no_record_of_the_key()
    {
        using var db = new PaymentDatabase();
        var gateway = new StaticQueryGateway("Alpha", GatewayResultType.Error, "not_found");
        Guid paymentId = await SeedStaleProcessingPayment(db, "key-missing");

        using PayMaestroDbContext recoveryContext = db.NewContext();
        await db.NewRecovery(recoveryContext, gateway).Execute(Cutoff, take: 10);

        using PayMaestroDbContext verification = db.NewContext();
        Payment payment = await verification.Payment.SingleAsync(p => p.Id == paymentId);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public async Task Should_leave_no_payment_in_processing_when_a_batch_is_recovered()
    {
        using var db = new PaymentDatabase();
        var gateway = new StaticQueryGateway("Alpha", GatewayResultType.SoftDecline, "51");

        for (int index = 1; index <= 3; index++)
        {
            await SeedStaleProcessingPayment(db, $"key-batch-{index}");
        }

        using PayMaestroDbContext recoveryContext = db.NewContext();
        int recovered = await db.NewRecovery(recoveryContext, gateway).Execute(Cutoff, take: 10);

        using PayMaestroDbContext verification = db.NewContext();
        List<Payment> payments = await verification.Payment.ToListAsync();

        Assert.Equal(3, recovered);
        Assert.All(payments, payment => Assert.NotEqual(PaymentStatus.Processing, payment.Status));
    }

    [Fact]
    public async Task Should_keep_the_committed_outcome_when_another_payment_loses_a_concurrency_race()
    {
        using var db = new PaymentDatabase();
        await SeedStaleProcessingPayment(db, "key-first");
        await SeedStaleProcessingPayment(db, "key-second");

        var gateway = new StaticQueryGateway("Alpha", GatewayResultType.Approved, "00");

        // While the pass is settling its first payment, another writer settles the other one.
        // The second commit then meets a moved concurrency stamp.
        gateway.AfterQuery = async queriedKey =>
        {
            if (gateway.Queries != 1)
            {
                return;
            }

            using PayMaestroDbContext interloper = db.NewContext();
            Payment other = await interloper.Payment
                .Include(payment => payment.Attempts)
                .SingleAsync(payment => payment.Attempts.All(attempt =>
                    attempt.ProviderIdempotencyKey != queriedKey));

            other.Decline();
            await interloper.SaveChangesAsync();
        };

        using PayMaestroDbContext recoveryContext = db.NewContext();
        int recovered = await db.NewRecovery(recoveryContext, gateway).Execute(Cutoff, take: 10);

        using PayMaestroDbContext verification = db.NewContext();
        List<Payment> payments = await verification.Payment.ToListAsync();

        // The payment committed before the conflict keeps its outcome instead of being rolled
        // back with the batch, and the other writer's outcome is the one that stands.
        Assert.Equal(1, recovered);
        Assert.Single(payments, payment => payment.Status == PaymentStatus.Captured);
        Assert.Single(payments, payment => payment.Status == PaymentStatus.Declined);
    }

    private static DateTime Cutoff => DateTime.UtcNow.AddMinutes(1);

    /// <summary>A payment whose process stopped after committing the attempt.</summary>
    private static async Task<Guid> SeedStaleProcessingPayment(PaymentDatabase db, string idempotencyKey)
    {
        using PayMaestroDbContext context = db.NewContext();

        Payment payment = TestPayment.Reserved(idempotencyKey: idempotencyKey);
        string providerKey = ProviderIdempotencyKey.For(payment, "Alpha", 1);
        payment.RecordAttempt(PaymentAttempt.Start(payment.Id, "Alpha", 1, providerKey));

        context.Payment.Add(payment);
        await context.SaveChangesAsync();

        return payment.Id;
    }
}
