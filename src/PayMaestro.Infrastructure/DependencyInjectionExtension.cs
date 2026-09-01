using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.Payments;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentGateways;

namespace PayMaestro.Infrastructure;

public static class DependencyInjectionExtension
{
    internal const string DefaultConnectionString = "Data Source=paymaestro.db";

    private const string ConnectionStringName = "DefaultConnection";

    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName) ?? DefaultConnectionString;

        services.AddDbContext<PayMaestroDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IPaymentReadOnlyRepository, PaymentRepository>();
        services.AddScoped<IPaymentWriteOnlyRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // The mock acquirers share one ledger so a key they already settled is recognised
        // across requests, the way a real provider's idempotency contract behaves.
        services.AddSingleton<MockProviderLedger>();
        services.AddScoped<IPaymentGateway, AlphaPayGateway>();
        services.AddScoped<IPaymentGateway, BetaPayGateway>();
        services.AddScoped<IPaymentGateway, GammaPayGateway>();
    }
}
