using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayMaestro.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PayMaestroDbContext>
{
    public PayMaestroDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<PayMaestroDbContext> options = new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite(DependencyInjectionExtension.DefaultConnectionString)
            .Options;

        return new PayMaestroDbContext(options);
    }
}
