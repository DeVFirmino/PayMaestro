using Microsoft.OpenApi;
using PayMaestro.API.Filters;
using PayMaestro.Application.Options;
using PayMaestro.Application.Services;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>());
builder.Services.Configure<GatewayRoutingOptions>(builder.Configuration.GetSection("GatewayRouting"));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PayMaestro API",
        Version = "v1",
        Description = "Payment orchestration API: multi-gateway cascade with automatic failover, "
                    + "idempotency protection and fraud velocity screening."
    });

    // Pull the /// comments from controllers and DTOs into the Swagger UI.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PayMaestro.API.xml"));
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<PaymentOrchestrator>();
builder.Services.AddScoped<CascadeExecutor>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PayMaestroDbContext>();
    db.Database.EnsureCreated();     // demo-friendly; migrations in production
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PayMaestro API v1");
    options.RoutePrefix = string.Empty;   // serve Swagger UI at the root URL
});

app.MapControllers();

app.Run();