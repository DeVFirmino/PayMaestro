using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.Domain.Entities;

public sealed class Payment : EntityBase
{
    /// <summary>Only the BIN and the last four digits are ever stored; the full PAN never is.</summary>
    public const int CardBinLength = 6;

    public const int CardLast4Length = 4;

    private const int CurrencyCodeLength = 3;

    // EF Core materialises the entity through the private constructor and sets every
    // property afterwards, so the null-forgiving defaults never reach a caller.
    public string IdempotencyKey { get; private set; } = null!;

    public string MerchantReference { get; private set; } = null!;

    public string CustomerId { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = null!;

    public string CardBin { get; private set; } = null!;

    public string CardLast4 { get; private set; } = null!;

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

    /// <summary>The most recent gateway attempt, or null while no gateway has been contacted.</summary>
    public PaymentAttempt? LastAttempt => Attempts.MaxBy(attempt => attempt.AttemptOrder);

    private Payment()
    {
    }

    public static Payment Create(
        string idempotencyKey,
        string merchantReference,
        string customerId,
        decimal amount,
        string currency,
        string cardBin,
        string cardLast4,
        string cardCountry,
        string customerIp,
        string ipCountry)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationFailedException(ErrorMessages.IdempotencyKeyRequired);
        }

        if (amount <= 0)
        {
            throw new ValidationFailedException(ErrorMessages.AmountMustBePositive);
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != CurrencyCodeLength)
        {
            throw new ValidationFailedException(ErrorMessages.CurrencyMustBeIsoCode);
        }

        return new Payment
        {
            IdempotencyKey = idempotencyKey,
            MerchantReference = merchantReference,
            CustomerId = customerId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            CardBin = cardBin,
            CardLast4 = cardLast4,
            CardCountry = cardCountry,
            CustomerIp = customerIp,
            IpCountry = ipCountry,
            Status = PaymentStatus.Pending,
            UpdatedAt = DateTime.UtcNow,
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
        MarkModified();
    }

    public void RecordAttempt(PaymentAttempt attempt)
    {
        Attempts.Add(attempt);
        MarkModified();
    }

    public void AddFraudFlag(FraudFlag flag)
    {
        FraudFlags.Add(flag);
        MarkModified();
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
        MarkModified();
    }

    public void Capture()
    {
        if (Status is not PaymentStatus.Authorized)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Captured);
        }

        Status = PaymentStatus.Captured;
        MarkModified();
    }

    /// <summary>
    /// An approved charge is captured in the same step: this API has no separate capture
    /// call, so an approval from the provider settles the payment outright.
    /// </summary>
    public void AuthorizeAndCapture()
    {
        Authorize();
        Capture();
    }

    public void Decline()
    {
        if (Status is not (PaymentStatus.Processing or PaymentStatus.RequiresReconciliation))
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.Declined);
        }

        Status = PaymentStatus.Declined;
        MarkModified();
    }

    public void RejectAsFraud()
    {
        if (Status is not PaymentStatus.Processing)
        {
            throw new InvalidStateTransitionException(Status, PaymentStatus.FraudRejected);
        }

        Status = PaymentStatus.FraudRejected;
        MarkModified();
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
        MarkModified();
    }

    /// <summary>Rotates the concurrency stamp so that any writer holding the old one loses.</summary>
    private void MarkModified()
    {
        UpdatedAt = DateTime.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
