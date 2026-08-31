using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.Options;
using PayMaestro.Application.Services;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentRequests;
using Microsoft.Extensions.Configuration;

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
        context.Database.Migrate();
    }

    public PayMaestroDbContext NewContext() => new(
        new DbContextOptionsBuilder<PayMaestroDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    public CreatePaymentUseCase NewOrchestrator(
        PayMaestroDbContext context,
        IPaymentReadOnlyRepository? readRepo = null,
        IEnumerable<IFraudRule>? fraudRules = null,
        params IPaymentGateway[] gateways)
    {
        PaymentRepository repository = new(context);

        return new CreatePaymentUseCase(
            readRepo ?? repository, repository, new UnitOfWork(context),
            gateways, fraudRules ?? [], Routing(gateways), new GatewayCascade(new UnitOfWork(context)),
            FingerprintGenerator);
    }

    public ReconcilePaymentUseCase NewReconciler(PayMaestroDbContext context, params IPaymentGateway[] gateways)
        => new(new PaymentRepository(context), new UnitOfWork(context), gateways);

    public RecoverProcessingAttemptsUseCase NewRecovery(PayMaestroDbContext context, params IPaymentGateway[] gateways)
        => new(new PaymentRepository(context), new UnitOfWork(context), gateways);

    /// <summary>The keyed fingerprint generator, with the secret a test run may use openly.</summary>
    public static HmacPaymentRequestFingerprintGenerator FingerprintGenerator { get; } =
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HmacPaymentRequestFingerprintGenerator.SecretConfigurationKey] = "test-fingerprint-secret"
            })
            .Build());

    public static CreatePaymentRequest Request(decimal amount = 100m, string cardNumber = "4111111111117777")
        => new()
        {
            MerchantReference = "ORDER-1",
            CustomerId = "cust-1",
            Amount = amount,
            Currency = "EUR",
            CardNumber = cardNumber,
            CustomerIp = "203.0.113.10"
        };

    private static IOptions<GatewayRoutingOptions> Routing(IPaymentGateway[] gateways)
        => Options.Create(new GatewayRoutingOptions
        {
            Gateways = [.. gateways.Select((gateway, index) => new GatewayRouteOptions
            {
                Name = gateway.Name,
                Priority = index,
                SupportedCurrencies = ["EUR"],
                MaxAmount = 1_000_000m
            })]
        });

    public void Dispose()
    {
        SqliteConnection.ClearAllPools(); // release the file handle before deleting it
        File.Delete(_path);
    }
}
