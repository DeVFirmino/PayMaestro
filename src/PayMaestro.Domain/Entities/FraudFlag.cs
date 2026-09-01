namespace PayMaestro.Domain.Entities;

public sealed class FraudFlag : EntityBase
{
    // EF Core materialises the entity through the private constructor and sets every
    // property afterwards, so the null-forgiving defaults never reach a caller.
    public Guid PaymentId { get; private set; }

    public string RuleName { get; private set; } = null!;

    public string Details { get; private set; } = null!;

    private FraudFlag()
    {
    }

    public static FraudFlag Create(Guid paymentId, string ruleName, string details) => new()
    {
        PaymentId = paymentId,
        RuleName = ruleName,
        Details = details,
    };
}
