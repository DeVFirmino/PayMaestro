using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Application.Fraud;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.GetPaymentById;
using PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;
using PayMaestro.Domain.Fraud;

namespace PayMaestro.Application;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GatewayCascade>();
        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<IFraudRule, DeclineVelocityRule>();
        services.AddScoped<IGetPaymentByIdUseCase, GetPaymentByIdUseCase>();
        services.AddScoped<IRecoverProcessingAttemptsUseCase, RecoverProcessingAttemptsUseCase>();
        services.AddScoped<IReconcilePaymentUseCase, ReconcilePaymentUseCase>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
