using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PayMaestro.API.Filters;
using PayMaestro.API.Health;
using PayMaestro.API.Workers;
using PayMaestro.Application;
using PayMaestro.Application.Options;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>())
    // An empty status result must reach the client as-is: GET of an unknown payment answers
    // 404 with an empty body, not a synthesized ProblemDetails envelope.
    .ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true);
builder.Services.Configure<GatewayRoutingOptions>(builder.Configuration.GetSection("GatewayRouting"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Left unset, the scheme reads its issuer, audience and signing keys from the
        // Authentication:Schemes:Bearer section, which is what "dotnet user-jwts" writes.
        string? authority = builder.Configuration["Authentication:Authority"];
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
            options.Audience = builder.Configuration["Authentication:Audience"];
        }

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("payments:write", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "payments:write"));
    options.AddPolicy("payments:read", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "payments:read", "payments:write"));
    options.AddPolicy("payments:reconcile", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "payments:reconcile"));
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-merchant", httpContext =>
    {
        string merchantId = httpContext.User.FindFirstValue("merchant_id")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(merchantId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "The merchant rate limit was exceeded."
        }, cancellationToken);
    };
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PayMaestro API",
        Version = "v1",
        Description = "Payment orchestration API: multi-gateway cascade with automatic failover, "
                    + "completed-outcome replay and fraud velocity screening."
    });

    // Pull the /// comments from controllers and DTOs into the Swagger UI.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PayMaestro.API.xml"));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHostedService<PaymentAttemptRecoveryWorker>();

WebApplication app = builder.Build();

// Local development gets a database without a separate step. Every other environment applies
// the migrations before the application starts, with the bundle described in the README, so a
// deployment never changes the schema from inside the running application.
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    PayMaestroDbContext database = scope.ServiceProvider.GetRequiredService<PayMaestroDbContext>();
    database.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PayMaestro API v1"));

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program { } // exposes the entry point to WebApplicationFactory in the tests
