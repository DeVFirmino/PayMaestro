using PayMaestro.Domain.Repositories;

namespace PayMaestro.Tests.Support;

/// <summary>A unit of work for a test that holds no database: committing changes nothing.</summary>
public sealed class NullUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
