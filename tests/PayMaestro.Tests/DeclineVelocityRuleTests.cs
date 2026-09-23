using PayMaestro.Application.Fraud;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Fraud;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

public sealed class DeclineVelocityRuleTests
{
    private const string FirstCard = "4111111111117777";
    private const string SameBinAndLastFour = "4111119999997777";

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task ShouldBlockOnlyWhenCardHasThreeRecentDeclines(int declineCount, bool shouldBlock)
    {
        using PaymentDatabase db = new();
        await SaveDeclinesAsync(db, FirstCard, declineCount, DateTime.UtcNow.AddHours(-1));

        FraudVerdict verdict = await EvaluateAsync(db, FirstCard);

        Assert.Equal(shouldBlock, verdict.IsSuspicious);
    }

    [Fact]
    public async Task ShouldIgnoreDeclinesOutsideThe24HourWindow()
    {
        using PaymentDatabase db = new();
        await SaveDeclinesAsync(db, FirstCard, 1, DateTime.UtcNow.AddHours(-25));
        await SaveDeclinesAsync(db, FirstCard, 2, DateTime.UtcNow.AddHours(-1));

        FraudVerdict verdict = await EvaluateAsync(db, FirstCard);

        Assert.False(verdict.IsSuspicious);
    }

    [Fact]
    public async Task ShouldKeepCardsSeparateWhenBinAndLastFourMatch()
    {
        using PaymentDatabase db = new();
        await SaveDeclinesAsync(db, SameBinAndLastFour, 3, DateTime.UtcNow.AddHours(-1));

        FraudVerdict verdict = await EvaluateAsync(db, FirstCard);

        Assert.False(verdict.IsSuspicious);
    }

    private static async Task<FraudVerdict> EvaluateAsync(PaymentDatabase db, string cardNumber)
    {
        using PayMaestroDbContext context = db.NewContext();
        DeclineVelocityRule rule = new(new PaymentRepository(context));

        return await rule.EvaluateAsync(NewPayment(cardNumber), CancellationToken.None);
    }

    private static async Task SaveDeclinesAsync(
        PaymentDatabase db,
        string cardNumber,
        int count,
        DateTime createdAt)
    {
        using PayMaestroDbContext context = db.NewContext();

        for (int index = 0; index < count; index++)
        {
            Payment payment = NewPayment(cardNumber);
            PaymentAttempt attempt = PaymentAttempt.Create(
                payment.Id,
                "AlphaPay",
                1,
                GatewayResultType.HardDecline,
                "DECLINED",
                1,
                Guid.NewGuid().ToString());
            payment.RecordAttempt(attempt);
            context.Payments.Add(payment);
            context.Entry(attempt).Property(nameof(EntityBase.CreatedAt)).CurrentValue = createdAt;
        }

        await context.SaveChangesAsync();
    }

    private static Payment NewPayment(string cardNumber) => Payment.Create(
        idempotencyKey: Guid.NewGuid().ToString(),
        merchantReference: "ORDER-1",
        customerId: "cust-1",
        amount: 100m,
        currency: "EUR",
        cardBin: cardNumber[..Payment.CardBinLength],
        cardLast4: cardNumber[^Payment.CardLast4Length..],
        cardFingerprint: PaymentDatabase.CardFingerprinter.Fingerprint(cardNumber),
        cardCountry: "MT",
        customerIp: "203.0.113.10",
        ipCountry: "MT");
}
