using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayMaestro.Infrastructure.Data;

namespace PayMaestro.Tests;

/// <summary>
/// The merchant-scoping migration deletes rows written before payments were scoped per
/// merchant: they belong to no merchant, and no backfill could attribute them truthfully. Test
/// databases are born empty, so this contract needs a database with actual pre-scoping history.
/// </summary>
public sealed class MigrationTests : IDisposable
{
    private const string LastPreScopingMigration = "20260825173633_AddIdempotencyReservationAndReconciliation";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"paymaestro-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public void Should_delete_pre_scoping_rows_when_the_merchant_scoping_migration_runs()
    {
        using PayMaestroDbContext context = NewContext();

        context.GetService<IMigrator>().Migrate(LastPreScopingMigration);
        context.Database.ExecuteSqlRaw(
            """
            INSERT INTO Payment (Id, IdempotencyKey, MerchantReference, CustomerId, Amount,
                Currency, CardBin, CardLast4, CardCountry, CustomerIp, IpCountry, Status,
                UpdatedAt, CreatedAt, ConcurrencyStamp)
            VALUES ('11111111-1111-1111-1111-111111111111', 'pre-scoping-key', 'ORDER-1',
                'cust-1', '100.0', 'EUR', '411111', '7777', 'MT', '203.0.113.10', 'MT',
                'Captured', '2026-08-01 12:00:00', '2026-08-01 12:00:00',
                '22222222-2222-2222-2222-222222222222');
            """);

        context.Database.Migrate();

        Assert.Equal(0, context.Payment.Count());
    }

    private PayMaestroDbContext NewContext() => new(
        new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
