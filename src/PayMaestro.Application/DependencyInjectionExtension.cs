using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Application.Fraud;
using PayMaestro.Application.Services;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.GetPaymentById;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;
using PayMaestro.Domain.Fraud;

namespace PayMaestro.Application;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CascadeExecutor>();
        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<IFraudRule, DeclineVelocityRule>();
        services.AddScoped<IGetPaymentByIdUseCase, GetPaymentByIdUseCase>();
        services.AddScoped<IReconcilePaymentUseCase, ReconcilePaymentUseCase>();

        return services;
    }
}
