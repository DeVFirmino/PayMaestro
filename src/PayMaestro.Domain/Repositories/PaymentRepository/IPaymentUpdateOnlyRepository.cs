using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentUpdateOnlyRepository
{
    /// <summary>
    /// Payments whose newest attempt has been in Processing since before the cutoff. The caller
    /// settles them, so this belongs to the update capability: the entities it returns are
    /// tracked and are meant to be changed.
    /// </summary>
    public Task<IReadOnlyList<Payment>> ListWithStaleProcessingAttemptsAsync(
        DateTime cutoff,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Payments stuck in Processing since before the cutoff with no attempt at all: the request
    /// that reserved the key failed before its cascade committed a first attempt. No gateway
    /// was ever contacted for them — the attempt row is committed before any call.
    /// </summary>
    public Task<IReadOnlyList<Payment>> ListStaleProcessingWithoutAttemptsAsync(
        DateTime cutoff,
        int take,
        CancellationToken cancellationToken);

    public void Update(Payment payment);
}
