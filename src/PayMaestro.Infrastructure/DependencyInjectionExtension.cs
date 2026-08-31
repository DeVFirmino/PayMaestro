using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentGateways;
using PayMaestro.Infrastructure.PaymentRequests;
using PayMaestro.Application.Services;

namespace PayMaestro.Infrastructure;

public static class DependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PayMaestroDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                          ?? "Data Source=paymaestro.db"));

        services.AddScoped<IPaymentReadOnlyRepository, PaymentRepository>();
        services.AddScoped<IPaymentUpdateOnlyRepository, PaymentRepository>();
        services.AddScoped<IPaymentWriteOnlyRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPaymentRequestFingerprintGenerator, HmacPaymentRequestFingerprintGenerator>();

        // The mock acquirers share one ledger so a key they already settled is recognised
        // across requests, the way a real provider's idempotency contract behaves.
        services.AddSingleton<MockProviderLedger>();
        services.AddScoped<IPaymentGateway, AlphaPayGateway>();
        services.AddScoped<IPaymentGateway, BetaPayGateway>();
        services.AddScoped<IPaymentGateway, GammaPayGateway>();
    }
}
