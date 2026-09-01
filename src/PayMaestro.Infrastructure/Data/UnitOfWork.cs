using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Repositories;

namespace PayMaestro.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private const int SqliteConstraintErrorCode = 19;

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
        catch (DbUpdateConcurrencyException exception)
        {
            // The payment's concurrency stamp moved: another writer settled it first.
            throw new ConcurrentPaymentModificationException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new UniqueConstraintViolationException(exception);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintErrorCode };
}
