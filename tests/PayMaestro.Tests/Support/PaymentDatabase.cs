using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Options;
using PayMaestro.Application.Services;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;

namespace PayMaestro.Tests.Support;

/// <summary>
/// A real SQLite file, so the unique index on the idempotency key and the concurrency stamp are
/// enforced by the database instead of simulated. Each simulated request gets its own context,
/// the way a scoped lifetime would give it one in the API.
/// </summary>
public sealed class PaymentDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"paymaestro-{Guid.NewGuid():N}.db");

    public PaymentDatabase()
    {
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public PayMaestroDbContext NewContext() => new(
        new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    public PaymentOrchestrator NewOrchestrator(
        PayMaestroDbContext context,
        IPaymentReadOnlyRepository? readRepo = null,
        IEnumerable<IFraudRule>? fraudRules = null,
        params IPaymentGateway[] gateways)
    {
        var repository = new PaymentRepository(context);

        return new PaymentOrchestrator(
            readRepo ?? repository, repository, new UnitOfWork(context),
            gateways, fraudRules ?? [], Routing(gateways), new CascadeExecutor());
    }

    public PaymentReconciler NewReconciler(PayMaestroDbContext context, params IPaymentGateway[] gateways)
        => new(new PaymentRepository(context), new UnitOfWork(context), gateways);

    public static RequestCreatePaymentJson Request(decimal amount = 100m, string cardNumber = "4111111111117777")
        => new("ORDER-1", "cust-1", amount, "EUR", cardNumber, "203.0.113.10");

    private static IOptions<GatewayRoutingOptions> Routing(IPaymentGateway[] gateways)
        => Options.Create(new GatewayRoutingOptions
        {
            Gateways = [.. gateways.Select((g, index) => new GatewayRouteOptions
            {
                Name = g.Name,
                Priority = index,
                SupportedCurrencies = ["EUR"],
                MaxAmount = 1_000_000m
            })]
        });

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();   // release the file handle before deleting it
        File.Delete(_path);
    }
}
