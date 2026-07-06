using PayMaestro.Domain.Repositories;

namespace PayMaestro.Infrastructure.Data;

public class UnitOfWork(PayMaestroDbContext context) : IUnitOfWork
{
     public async Task Commit() => await context.SaveChangesAsync();
}