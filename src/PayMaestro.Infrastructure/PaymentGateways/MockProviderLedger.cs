using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;

namespace PayMaestro.Infrastructure.PaymentGateways;

/// <summary>
/// Stands in for what a real acquirer keeps on its own side: the outcome it already produced
/// for an idempotency key. It is what makes the mock gateways honest about the contract —
/// presenting a key twice returns the first outcome instead of charging twice.
/// </summary>
/// <remarks>
/// The records are stored in the database, so they survive a restart the way a provider's do.
/// Each call opens a context of its own and saves at once: a provider records the charge the
/// moment it takes the money, whether or not the orchestrator's own save succeeds afterwards.
/// </remarks>
public sealed class MockProviderLedger
{
    private const int SqliteConstraintErrorCode = 19;

    private readonly IDbContextFactory<PayMaestroDbContext> _contexts;

    public MockProviderLedger(IDbContextFactory<PayMaestroDbContext> contexts)
    {
        _contexts = contexts;
    }

    /// <summary>Stores the outcome for a key, or returns the one already stored.</summary>
    public async Task<GatewayResult> SettleAsync(
        string providerIdempotencyKey,
        GatewayResult result,
        CancellationToken cancellationToken)
    {
        if (await TryRecordAsync(providerIdempotencyKey, result, cancellationToken))
        {
            return result;
        }

        // A concurrent call with the same key recorded first, and the provider keeps that outcome.
        return await FindAsync(providerIdempotencyKey, cancellationToken)
            ?? throw new InvalidOperationException($"The ledger refused key '{providerIdempotencyKey}' but holds no record of it.");
    }

    public async Task<GatewayResult?> FindAsync(string providerIdempotencyKey, CancellationToken cancellationToken)
    {
        await using PayMaestroDbContext context = await _contexts.CreateDbContextAsync(cancellationToken);

        ProviderLedgerRecord? record = await context.ProviderLedger
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.ProviderIdempotencyKey == providerIdempotencyKey, cancellationToken);

        return record?.ToResult();
    }

    /// <summary>Returns false when the key already has a record: the primary key refused the second one.</summary>
    private async Task<bool> TryRecordAsync(
        string providerIdempotencyKey,
        GatewayResult result,
        CancellationToken cancellationToken)
    {
        await using PayMaestroDbContext context = await _contexts.CreateDbContextAsync(cancellationToken);
        context.ProviderLedger.Add(ProviderLedgerRecord.Create(providerIdempotencyKey, result));

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintErrorCode })
        {
            return false;
        }
    }
}
