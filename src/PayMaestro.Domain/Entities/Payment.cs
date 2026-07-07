using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.Domain.Entities;

public class Payment : EntityBase
{
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

    public List<PaymentAttempt> Attempts { get; private set; } = [];
    public List<FraudFlag> FraudFlags { get; private set; } = [];

    private Payment() { } // EF Core

    public static Payment Create(string idempotencyKey, string merchantReference,
        string customerId, decimal amount, string currency, string cardBin,
        string cardLast4, string cardCountry, string customerIp, string ipCountry)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

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
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void RecordAttempt(PaymentAttempt attempt)
    {
        Attempts.Add(attempt);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddFraudFlag(FraudFlag flag)
    {
        FraudFlags.Add(flag);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Authorize()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidStateTransitionException(Status, PaymentStatus.Authorized);

        Status = PaymentStatus.Authorized;
        UpdatedAt = DateTime.UtcNow;
    }
 
    public void Capture()
    {
        if (Status != PaymentStatus.Authorized)
            throw new InvalidStateTransitionException(Status, PaymentStatus.Captured);

        Status = PaymentStatus.Captured;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Decline()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidStateTransitionException(Status, PaymentStatus.Declined);

        Status = PaymentStatus.Declined;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RejectAsFraud()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidStateTransitionException(Status, PaymentStatus.FraudRejected);

        Status = PaymentStatus.FraudRejected;
        UpdatedAt = DateTime.UtcNow;
    }

}
