using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

public sealed class PaymentTests
{
    [Fact]
    public void ShouldStartPendingWhenCreated()
        => Assert.Equal(PaymentStatus.Pending, new PaymentBuilder().Build().Status);

    [Fact]
    public void ShouldMoveToProcessingWhenKeyIsReserved()
    {
        Payment payment = new PaymentBuilder().Build();

        payment.BeginProcessing();

        Assert.Equal(PaymentStatus.Processing, payment.Status);
    }

    [Fact]
    public void ShouldRejectAuthorizationWhenKeyIsNotReserved()
        => Assert.Throws<InvalidStateTransitionException>(new PaymentBuilder().Build().Authorize);

    [Fact]
    public void ShouldCaptureWhenAuthorizedFirst()
    {
        Payment payment = new PaymentBuilder().BuildReserved();

        payment.Authorize();
        payment.Capture();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public void ShouldRejectCaptureWhenNotAuthorized()
        => Assert.Throws<InvalidStateTransitionException>(new PaymentBuilder().BuildReserved().Capture);

    [Fact]
    public void ShouldRejectDeclineWhenAlreadyCaptured()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        payment.AuthorizeAndCapture();

        Assert.Throws<InvalidStateTransitionException>(payment.Decline);
    }

    [Fact]
    public void ShouldSettleEitherWayWhenProviderAnswersUnknownOutcome()
    {
        Payment captured = new PaymentBuilder().BuildReserved();
        captured.MarkForReconciliation();
        captured.AuthorizeAndCapture();

        Payment declined = new PaymentBuilder().BuildReserved();
        declined.MarkForReconciliation();
        declined.Decline();

        Assert.Equal(PaymentStatus.Captured, captured.Status);
        Assert.Equal(PaymentStatus.Declined, declined.Status);
    }

    [Fact]
    public void ShouldRejectReconciliationWhenAlreadySettled()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        payment.Decline();

        Assert.Throws<InvalidStateTransitionException>(payment.MarkForReconciliation);
    }

    [Fact]
    public void ShouldRotateConcurrencyStampWhenStateChanges()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        Guid beforeAuthorize = payment.ConcurrencyStamp;

        payment.Authorize();

        Assert.NotEqual(beforeAuthorize, payment.ConcurrencyStamp);
    }

    [Fact]
    public void ShouldRecordTriggeredRuleWhenRejectedAsFraud()
    {
        Payment payment = new PaymentBuilder().BuildReserved();

        payment.AddFraudFlag(FraudFlag.Create(payment.Id, "DeclineVelocity", "3 declines in 24h"));
        payment.RejectAsFraud();

        Assert.Equal(PaymentStatus.FraudRejected, payment.Status);
        Assert.Single(payment.FraudFlags);
        Assert.Equal("DeclineVelocity", payment.FraudFlags[0].RuleName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ShouldRejectCreationWhenAmountIsNotPositive(decimal amount)
    {
        ValidationFailedException exception = Assert.Throws<ValidationFailedException>(
            () => new PaymentBuilder().WithAmount(amount).Build());

        Assert.Equal(ErrorMessages.AmountMustBePositive, exception.Message);
    }
}
