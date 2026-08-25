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

    public async Task<Payment?> GetByIdempotencyKey(string idempotencyKey)
    {
        var result = await inner.GetByIdempotencyKey(idempotencyKey);

        if (!_held)
        {
            _held = true;
            hasRead.TrySetResult();
            await release.Task;
        }

        return result;
    }

    public Task<Payment?> GetById(Guid id) => inner.GetById(id);

    public Task<int> CountRecentDeclinedAttempts(string cardBin, string cardLast4, TimeSpan window)
        => inner.CountRecentDeclinedAttempts(cardBin, cardLast4, window);
}
