using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayMaestro.Application.Options;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories.Payments;
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
        using PayMaestroDbContext context = NewContext();
        context.Database.EnsureCreated();
    }

    public PayMaestroDbContext NewContext() => new(
        new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    public CreatePaymentUseCase NewCreatePaymentUseCase(
        PayMaestroDbContext context,
        IPaymentReadOnlyRepository? reader = null,
        IEnumerable<IFraudRule>? fraudRules = null,
        params IPaymentGateway[] gateways)
    {
        PaymentRepository repository = new(context);

        return new CreatePaymentUseCase(
            reader ?? repository,
            repository,
            new UnitOfWork(context),
            fraudRules ?? [],
            new GatewayRouter(Routing(gateways), gateways),
            new CascadeExecutor());
    }

    public ReconcilePaymentUseCase NewReconcilePaymentUseCase(
        PayMaestroDbContext context,
        params IPaymentGateway[] gateways)
        => new(new PaymentRepository(context), new UnitOfWork(context), gateways);

    /// <summary>Reads committed state only, on a connection of its own.</summary>
    public Payment? FindCommittedByKey(string idempotencyKey)
    {
        using PayMaestroDbContext observer = NewContext();

        return observer.Payments.AsNoTracking()
            .FirstOrDefault(payment => payment.IdempotencyKey == idempotencyKey);
    }

    public async Task<int> CountPaymentsWithKeyAsync(string idempotencyKey)
    {
        using PayMaestroDbContext context = NewContext();

        return await context.Payments.CountAsync(payment => payment.IdempotencyKey == idempotencyKey);
    }

    public async Task<PaymentAttempt> SingleAttemptOfAsync(Guid paymentId)
    {
        using PayMaestroDbContext context = NewContext();

        return await context.PaymentAttempts.SingleAsync(attempt => attempt.PaymentId == paymentId);
    }

    /// <summary>Loads the payment into the given context, so that context now holds its current stamp.</summary>
    public static Task<Payment> LoadIntoAsync(PayMaestroDbContext context, Guid paymentId)
        => context.Payments.Include(payment => payment.Attempts).FirstAsync(payment => payment.Id == paymentId);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();   // release the file handle before deleting it
        File.Delete(_path);
    }

    private static IOptions<GatewayRoutingOptions> Routing(IPaymentGateway[] gateways)
        => Options.Create(new GatewayRoutingOptions
        {
            Gateways = [.. gateways.Select((gateway, index) => new GatewayRouteOptions
            {
                Name = gateway.Name,
                Priority = index,
                SupportedCurrencies = ["EUR"],
                MaxAmount = 1_000_000m,
            })],
        });
}
