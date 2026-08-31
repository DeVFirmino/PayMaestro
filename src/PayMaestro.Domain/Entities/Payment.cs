using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.ValueObjects;

namespace PayMaestro.Domain.Entities;

public sealed class Payment : EntityBase
{
    public string MerchantId { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestFingerprint { get; private set; } = null!;
    public string MerchantReference { get; private set; } = null!;
    public string CustomerId { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string CardBin { get; private set; } = null!;
    public string CardLast4 { get; private set; } = null!;
    public string PaymentMethodToken { get; private set; } = null!;
    public string CardCountry { get; private set; } = null!;
    public string CustomerIp { get; private set; } = null!;
    public string IpCountry { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Every transition changes it, so a writer holding
    /// a stale copy of the payment loses instead of overwriting a newer outcome.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    public List<PaymentAttempt> Attempts { get; private set; } = [];
    public List<FraudFlag> FraudFlags { get; private set; } = [];

    /// <summary>The outcome is settled: replaying this payment must never call a gateway again.</summary>
    public bool IsTerminal => Status is PaymentStatus.Captured or PaymentStatus.Declined
        or PaymentStatus.FraudRejected or PaymentStatus.Refunded;

    private Payment() { } // EF Core

    public static Payment Create(string merchantId, string idempotencyKey, string requestFingerprint,
        string merchantReference,
        string customerId, decimal amount, string currency, string cardBin,
        string cardLast4, string paymentMethodToken, string cardCountry, string customerIp, string ipCountry)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            throw new ArgumentException("Merchant id is required.", nameof(merchantId));
        }

        IdempotencyKey validatedKey = ValueObjects.IdempotencyKey.Create(idempotencyKey);
        RequestFingerprint validatedFingerprint = ValueObjects.RequestFingerprint.Create(requestFingerprint);
        Money money = Money.Create(amount, currency);
        PaymentMethodToken validatedPaymentMethodToken = ValueObjects.PaymentMethodToken.Create(paymentMethodToken);

        return new Payment
        {
            MerchantId = merchantId,
            IdempotencyKey = validatedKey.Value,
            RequestFingerprint = validatedFingerprint.Value,
            MerchantReference = merchantReference,
            CustomerId = customerId,
            Amount = money.Amount,
            Currency = money.Currency,
            CardBin = cardBin,
            CardLast4 = cardLast4,
            PaymentMethodToken = validatedPaymentMethodToken.Value,
            CardCountry = cardCountry,
            CustomerIp = customerIp,
            IpCountry = ipCountry,
            Status = PaymentStatus.Pending,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Claims the idempotency key before any gateway is contacted. The row is inserted in
    /// this state, so a second request carrying the same key finds it instead of charging again.
    /// </summary>
    public void BeginProcessing()
    {
        if (Status is not PaymentStatus.Pending)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Processing);
        }

        Status = PaymentStatus.Processing;
        Touch();
    }

    public void RecordAttempt(PaymentAttempt attempt)
    {
        Attempts.Add(attempt);
        Touch();
    }

    public void AddFraudFlag(FraudFlag flag)
    {
        FraudFlags.Add(flag);
        Touch();
    }

    public void Authorize()
    {
        // Reconciliation resolves an unknown outcome into the same settled states,
        // so it is a legal starting point as well as Processing.
        if (Status is not (PaymentStatus.Processing or PaymentStatus.RequiresReconciliation))
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Authorized);
        }

        Status = PaymentStatus.Authorized;
        Touch();
    }

    public void Capture()
    {
        if (Status is not PaymentStatus.Authorized)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Captured);
        }

        Status = PaymentStatus.Captured;
        Touch();
    }

    public void Decline()
    {
        if (Status is not (PaymentStatus.Processing or PaymentStatus.RequiresReconciliation))
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Declined);
        }

        Status = PaymentStatus.Declined;
        Touch();
    }

    public void RejectAsFraud()
    {
        if (Status is not PaymentStatus.Processing)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.FraudRejected);
        }

        Status = PaymentStatus.FraudRejected;
        Touch();
    }

    /// <summary>
    /// A gateway stopped answering mid-charge. The payment stays unsettled until the
    /// provider is queried, and no other gateway may be tried in the meantime.
    /// </summary>
    public void MarkForReconciliation()
    {
        if (Status is not PaymentStatus.Processing)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.RequiresReconciliation);
        }

        Status = PaymentStatus.RequiresReconciliation;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
