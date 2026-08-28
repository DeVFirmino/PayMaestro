namespace PayMaestro.Domain.Repositories;

public interface IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken);
}
