using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.Tests;

public class PaymentTests
{
    [Fact]
    public void New_payment_starts_pending()
        => Assert.Equal(PaymentStatus.Pending, NewPayment().Status);

    [Fact]
    public void Authorize_then_capture_is_the_happy_path()
    {
        var payment = NewPayment();

        payment.Authorize();
        payment.Capture();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public void Capture_without_authorization_is_rejected()
        => Assert.Throws<InvalidStateTransitionException>(() => NewPayment().Capture());

    [Fact]
    public void Captured_payment_cannot_be_declined()
    {
        var payment = NewPayment();
        payment.Authorize();
        payment.Capture();

        Assert.Throws<InvalidStateTransitionException>(payment.Decline);
    }

    [Fact]
    public void Fraud_rejection_records_the_triggered_rule()
    {
        var payment = NewPayment();

        payment.AddFraudFlag(FraudFlag.Create(payment.Id, "DeclineVelocity", "3 declines in 24h"));
        payment.RejectAsFraud();

        Assert.Equal(PaymentStatus.FraudRejected, payment.Status);
        Assert.Single(payment.FraudFlags);
        Assert.Equal("DeclineVelocity", payment.FraudFlags[0].RuleName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Amount_must_be_positive(decimal amount)
        => Assert.Throws<ArgumentException>(() => Payment.Create(
            "key", "ref", "cust", amount, "EUR", "411111", "9999", "MT", "203.0.113.10", "MT"));

    private static Payment NewPayment() => Payment.Create(
        idempotencyKey: "key-1", merchantReference: "ORDER-1", customerId: "cust-1",
        amount: 100m, currency: "EUR", cardBin: "411111", cardLast4: "9999",
        cardCountry: "MT", customerIp: "203.0.113.10", ipCountry: "MT");
}