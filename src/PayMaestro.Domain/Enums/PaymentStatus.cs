namespace PayMaestro.Domain.Enums;

public enum PaymentStatus
{
    Pending, 
    FraudRejected, 
    Declined, 
    Authorized, 
    Captured, 
    Refunded
}

