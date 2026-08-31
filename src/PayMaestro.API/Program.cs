using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PayMaestro.API.Filters;
using PayMaestro.API.Health;
using PayMaestro.API.Security;
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
builder.Services.AddPaymentAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddPaymentAuthorization();
builder.Services.AddPaymentRateLimiting();

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
