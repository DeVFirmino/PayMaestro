namespace PayMaestro.Domain.Repositories;

public interface IUnitOfWork
{
    Task Commit();
}