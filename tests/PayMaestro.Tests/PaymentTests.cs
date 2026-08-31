using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

public class PaymentTests
{
    [Fact]
    public void New_payment_starts_pending()
        => Assert.Equal(PaymentStatus.Pending, NewPayment().Status);

    [Fact]
    public void Reserving_the_key_moves_the_payment_to_processing()
    {
        var payment = NewPayment();

        payment.BeginProcessing();

        Assert.Equal(PaymentStatus.Processing, payment.Status);
    }

    [Fact]
    public void A_payment_cannot_be_authorized_before_its_key_is_reserved()
        => Assert.Throws<InvalidStateTransitionException>(NewPayment().Authorize);

    [Fact]
    public void Authorize_then_capture_is_the_happy_path()
    {
        var payment = ReservedPayment();

        payment.Authorize();
        payment.Capture();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public void Capture_without_authorization_is_rejected()
        => Assert.Throws<InvalidStateTransitionException>(ReservedPayment().Capture);

    [Fact]
    public void Captured_payment_cannot_be_declined()
    {
        var payment = ReservedPayment();
        payment.Authorize();
        payment.Capture();

        Assert.Throws<InvalidStateTransitionException>(payment.Decline);
    }

    [Fact]
    public void An_unknown_outcome_can_be_settled_either_way_once_the_provider_answers()
    {
        var captured = ReservedPayment();
        captured.MarkForReconciliation();
        captured.Authorize();
        captured.Capture();

        var declined = ReservedPayment();
        declined.MarkForReconciliation();
        declined.Decline();

        Assert.Equal(PaymentStatus.Captured, captured.Status);
        Assert.Equal(PaymentStatus.Declined, declined.Status);
    }

    [Fact]
    public void A_settled_payment_cannot_be_sent_back_for_reconciliation()
    {
        var payment = ReservedPayment();
        payment.Decline();

        Assert.Throws<InvalidStateTransitionException>(payment.MarkForReconciliation);
    }

    [Fact]
    public void Every_transition_moves_the_concurrency_stamp()
    {
        var payment = ReservedPayment();
        var beforeAuthorize = payment.ConcurrencyStamp;

        payment.Authorize();

        Assert.NotEqual(beforeAuthorize, payment.ConcurrencyStamp);
    }

    [Fact]
    public void Fraud_rejection_records_the_triggered_rule()
    {
        var payment = ReservedPayment();

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
        => Assert.Throws<ArgumentException>(() => TestPayment.New(amount: amount));

    private static Payment NewPayment() => TestPayment.New();

    private static Payment ReservedPayment()
    {
        var payment = NewPayment();
        payment.BeginProcessing();
        return payment;
    }
}
