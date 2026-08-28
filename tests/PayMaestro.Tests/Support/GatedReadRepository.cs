using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Tests.Support;

/// <summary>
/// Holds a request right after its first idempotency lookup, so a test can reproduce the real
/// race deterministically: both requests look the key up, find nothing, and only then race to
/// insert it. Later lookups (the one that finds the winner) pass straight through.
/// </summary>
public sealed class GatedReadRepository(
    IPaymentReadOnlyRepository inner,
    TaskCompletionSource hasRead,
    TaskCompletionSource release) : IPaymentReadOnlyRepository
{
    private bool _held;

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        Payment? result = await inner.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (!_held)
        {
            _held = true;
            hasRead.TrySetResult();
            await release.Task;
        }

        return result;
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => inner.GetByIdAsync(id, cancellationToken);

    public Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        TimeSpan window,
        CancellationToken cancellationToken)
        => inner.CountRecentDeclinedAttemptsAsync(cardBin, cardLast4, window, cancellationToken);
}
