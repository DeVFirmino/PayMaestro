using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Repositories;

namespace PayMaestro.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PayMaestroDbContext _context;

    public UnitOfWork(PayMaestroDbContext context)
    {
        _context = context;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The payment's concurrency stamp moved: another writer settled it first.
            throw new ConcurrentPaymentModificationException();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Lets callers detect races on unique keys (e.g. two concurrent
            // requests with the same idempotency key) without referencing EF.
            throw new UniqueConstraintViolationException();
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqliteException { SqliteErrorCode: 19 }; // SQLITE_CONSTRAINT
}
