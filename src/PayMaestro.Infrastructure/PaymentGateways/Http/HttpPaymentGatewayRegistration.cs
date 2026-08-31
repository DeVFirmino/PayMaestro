using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways.Http;

/// <summary>
/// Registers one pair of HTTP clients for each configured acquirer. Each acquirer gets its own
/// timeouts and its own circuit breaker, so a slow or broken acquirer cannot hold up the others.
/// </summary>
public static class HttpPaymentGatewayRegistration
{
    public static void AddHttpPaymentGateways(
        this IServiceCollection services,
        PaymentProviderOptions provider,
        GatewayRoutingOptions routing)
    {
        foreach (GatewayRouteOptions route in routing.Gateways)
        {
            string gatewayName = route.Name;

            // The charge client never repeats a charge: one POST, one attempt.
            services.AddHttpClient(HttpPaymentGateway.ChargeClientName(gatewayName), client =>
                {
                    client.BaseAddress = new Uri(provider.BaseUrl);
                    client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns the deadline
                })
                .AddResilienceHandler($"charge:{gatewayName}", pipeline =>
                {
                    pipeline.AddTimeout(TimeSpan.FromSeconds(provider.TotalTimeoutSeconds));
                    pipeline.AddCircuitBreaker(CircuitBreaker(provider));
                    pipeline.AddTimeout(TimeSpan.FromSeconds(provider.AttemptTimeoutSeconds));
                });

            // The query client may repeat: asking what happened moves no money.
            services.AddHttpClient(HttpPaymentGateway.QueryClientName(gatewayName), client =>
                {
                    client.BaseAddress = new Uri(provider.BaseUrl);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .AddResilienceHandler($"query:{gatewayName}", pipeline =>
                {
                    pipeline.AddTimeout(TimeSpan.FromSeconds(provider.TotalTimeoutSeconds));
                    pipeline.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = provider.QueryRetryCount,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromMilliseconds(200)
                    });
                    pipeline.AddCircuitBreaker(CircuitBreaker(provider));
                    pipeline.AddTimeout(TimeSpan.FromSeconds(provider.AttemptTimeoutSeconds));
                });

            services.AddScoped<IPaymentGateway>(serviceProvider => new HttpPaymentGateway(
                gatewayName,
                serviceProvider.GetRequiredService<IHttpClientFactory>()));
        }
    }

    private static HttpCircuitBreakerStrategyOptions CircuitBreaker(PaymentProviderOptions provider)
        => new()
        {
            FailureRatio = provider.CircuitBreakerFailureRatio,
            MinimumThroughput = provider.CircuitBreakerMinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(provider.CircuitBreakerSamplingSeconds),
            BreakDuration = TimeSpan.FromSeconds(provider.CircuitBreakerBreakSeconds)
        };
}
