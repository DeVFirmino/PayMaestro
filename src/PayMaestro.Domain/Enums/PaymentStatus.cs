namespace PayMaestro.Domain.Enums;

public enum PaymentStatus
{
    /// <summary>Created in memory. Never persisted: the row is inserted already Processing.</summary>
    Pending,

    /// <summary>The idempotency key is reserved and gateway work may be in flight.</summary>
    Processing,

    /// <summary>A gateway did not answer. Whether it charged is unknown until reconciliation.</summary>
    RequiresReconciliation,

    FraudRejected,
    Declined,
    Authorized,
    Captured,
    Refunded
}
