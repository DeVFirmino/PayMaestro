using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Repositories;

namespace PayMaestro.Infrastructure.Data;

public class UnitOfWork(PayMaestroDbContext context) : IUnitOfWork
{
    public async Task Commit()
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // The payment's concurrency stamp moved: another writer settled it first.
            throw new ConcurrentPaymentModificationException();
        }
        catch (DbUpdateException e) when (IsUniqueConstraintViolation(e))
        {
            // Lets callers detect races on unique keys (e.g. two concurrent
            // requests with the same idempotency key) without referencing EF.
            throw new UniqueConstraintViolationException();
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException e)
        => e.InnerException is SqliteException { SqliteErrorCode: 19 };   // SQLITE_CONSTRAINT
}