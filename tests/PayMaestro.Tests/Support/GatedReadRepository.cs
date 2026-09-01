using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Tests.Support;

/// <summary>
/// Holds a request right after its first idempotency lookup, so a test can reproduce the real
/// race deterministically: both requests look the key up, find nothing, and only then race to
/// insert it. Later lookups (the one that finds the winner) pass straight through.
/// </summary>
public sealed class GatedReadRepository : IPaymentReadOnlyRepository
{
    private readonly IPaymentReadOnlyRepository _inner;
    private readonly TaskCompletionSource _hasRead;
    private readonly TaskCompletionSource _release;
    private bool _held;

    public GatedReadRepository(
        IPaymentReadOnlyRepository inner,
        TaskCompletionSource hasRead,
        TaskCompletionSource release)
    {
        _inner = inner;
        _hasRead = hasRead;
        _release = release;
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        Payment? result = await _inner.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (_held is false)
        {
            _held = true;
            _hasRead.TrySetResult();
            await _release.Task;
        }

        return result;
    }

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken)
        => _inner.GetByIdAsync(paymentId, cancellationToken);

    public Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        TimeSpan window,
        CancellationToken cancellationToken)
        => _inner.CountRecentDeclinedAttemptsAsync(cardBin, cardLast4, window, cancellationToken);
}
