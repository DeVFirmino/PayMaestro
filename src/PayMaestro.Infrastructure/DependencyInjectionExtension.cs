using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.Data.Repositories;
using PayMaestro.Infrastructure.PaymentGateways;

namespace PayMaestro.Infrastructure;

public static class DependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PayMaestroDbContext>(opt =>
            opt.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                          ?? "Data Source=paymaestro.db"));

        services.AddScoped<IPaymentReadOnlyRepository, PaymentRepository>();
        services.AddScoped<IPaymentWriteOnlyRepository, PaymentRepository>();
        services.AddScoped<IPaymentUpdateOnlyRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPaymentGateway, AlphaPayGateway>();
        services.AddScoped<IPaymentGateway, BetaPayGateway>();
        services.AddScoped<IPaymentGateway, GammaPayGateway>();
    }
}