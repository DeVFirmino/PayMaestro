using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PayMaestro.Infrastructure.Data;

namespace PayMaestro.Infrastructure.DataAccess;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PayMaestroDbContext>
{
    public PayMaestroDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite("Data Source=paymaestro.db")
            .Options;
        return new PayMaestroDbContext(options);
    }
}