using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PayMaestro.Infrastructure.Data;

namespace PayMaestro.API.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly PayMaestroDbContext _context;

    public DatabaseHealthCheck(PayMaestroDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        bool canConnect = await _context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("Database connection is available.")
            : HealthCheckResult.Unhealthy("Database connection is unavailable.");
    }
}
