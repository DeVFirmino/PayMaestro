using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.Payments;

public interface IPaymentReadOnlyRepository
{
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Payments still Processing with no gateway attempt, reserved at or before the given instant:
    /// the ones whose request ended before anything about a gateway call was saved.
    /// </summary>
    Task<IReadOnlyList<Payment>> GetProcessingWithoutAttemptsAsync(
        DateTime reservedBefore,
        CancellationToken cancellationToken);

    Task<int> CountRecentDeclinedAttemptsAsync(
        string cardFingerprint,
        TimeSpan window,
        CancellationToken cancellationToken);
}
