using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayMaestro.Infrastructure.Data;

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