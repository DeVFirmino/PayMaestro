using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PayMaestro.API.Security;

/// <summary>
/// Registers who the caller is, what the caller may do, and how often. Kept out of Program.cs
/// so that adding a policy or changing a limit does not edit the application's entry point.
/// </summary>
public static class PaymentSecurityExtension
{
    /// <summary>The rate limiting policy the payment endpoints are placed behind.</summary>
    public const string PerMerchantRateLimitPolicy = "per-merchant";

    private const int RequestsPerWindow = 120;

    public static IServiceCollection AddPaymentAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Left unset, the scheme reads its issuer, audience and signing keys from the
                // Authentication:Schemes:Bearer section, which is what "dotnet user-jwts" writes.
                string? authority = configuration["Authentication:Authority"];
                if (string.IsNullOrWhiteSpace(authority) is false)
                {
                    options.Authority = authority;
                    options.Audience = configuration["Authentication:Audience"];
                }

                options.RequireHttpsMetadata = environment.IsDevelopment() is false;
            });

        services.AddSingleton<IClaimsTransformation, ScopeClaimTransformation>();

        return services;
    }

    public static IServiceCollection AddPaymentAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Every policy also demands a usable merchant identity: present, and not the
            // reserved legacy id. Enforced here, in authorization, so no endpoint can reach a
            // use case for a caller the platform cannot attribute.
            options.AddPolicy(PaymentPolicies.Write, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(ScopeClaimTransformation.ScopeClaimType, PaymentPolicies.Write)
                .RequireAssertion(HasUsableMerchantIdentity));

            // Writing implies reading: a merchant that may create a payment may read it back.
            options.AddPolicy(PaymentPolicies.Read, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    ScopeClaimTransformation.ScopeClaimType, PaymentPolicies.Read, PaymentPolicies.Write)
                .RequireAssertion(HasUsableMerchantIdentity));

            options.AddPolicy(PaymentPolicies.Reconcile, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(ScopeClaimTransformation.ScopeClaimType, PaymentPolicies.Reconcile)
                .RequireAssertion(HasUsableMerchantIdentity));
        });

        return services;
    }

    public static IServiceCollection AddPaymentRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PerMerchantRateLimitPolicy, PartitionByMerchant);
            options.OnRejected = RejectWithProblemDetails;
        });

        return services;
    }

    private static bool HasUsableMerchantIdentity(AuthorizationHandlerContext context)
        => MerchantIdentity.IsUsable(context.User);

    /// <summary>One window per merchant, so a noisy merchant cannot spend another's allowance.</summary>
    private static RateLimitPartition<string> PartitionByMerchant(HttpContext httpContext)
        => RateLimitPartition.GetFixedWindowLimiter(
            MerchantIdentity.Find(httpContext.User) ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RequestsPerWindow,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });

    private static ValueTask RejectWithProblemDetails(OnRejectedContext context, CancellationToken cancellationToken)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "The merchant rate limit was exceeded.",
            Instance = context.HttpContext.Request.Path
        }, cancellationToken));
    }
}
