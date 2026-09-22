namespace PayMaestro.Application.Options;

public sealed class PaymentRecoveryOptions
{
    public const string SectionName = "PaymentRecovery";

    /// <summary>
    /// How long a payment must have been Processing, with no attempt saved, before recovery treats
    /// its request as gone. It must be longer than any request can take: recovering a payment whose
    /// request is still charging would settle it before that request has its answer.
    /// </summary>
    public TimeSpan OrphanThreshold { get; set; } = TimeSpan.FromMinutes(15);
}
