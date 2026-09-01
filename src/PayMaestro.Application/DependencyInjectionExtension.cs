using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Application.Fraud;
using PayMaestro.Application.Options;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.GetPaymentById;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;
using PayMaestro.Domain.Fraud;

namespace PayMaestro.Application;

public static class DependencyInjectionExtension
{
    public static void AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayRoutingOptions>(configuration.GetSection(GatewayRoutingOptions.SectionName));

        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<IGetPaymentByIdUseCase, GetPaymentByIdUseCase>();
        services.AddScoped<IReconcilePaymentUseCase, ReconcilePaymentUseCase>();

        services.AddScoped<CascadeExecutor>();
        services.AddScoped<GatewayRouter>();
        services.AddScoped<IFraudRule, DeclineVelocityRule>();
    }
}
