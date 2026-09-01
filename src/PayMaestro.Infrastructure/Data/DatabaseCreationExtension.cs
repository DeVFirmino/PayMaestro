using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PayMaestro.Infrastructure.Data;

public static class DatabaseCreationExtension
{
    /// <summary>
    /// Creates the schema from the model on a fresh database. Meant for local runs only: it
    /// bypasses the migration history, so a database created this way cannot be migrated later.
    /// </summary>
    public static void EnsureDatabaseCreated(this IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();

        PayMaestroDbContext context = scope.ServiceProvider.GetRequiredService<PayMaestroDbContext>();
        context.Database.EnsureCreated();
    }
}
