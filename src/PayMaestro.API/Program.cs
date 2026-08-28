using Microsoft.OpenApi;
using PayMaestro.API.Filters;
using PayMaestro.Application;
using PayMaestro.Application.Options;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>());
builder.Services.Configure<GatewayRoutingOptions>(builder.Configuration.GetSection("GatewayRouting"));

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

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    PayMaestroDbContext db = scope.ServiceProvider.GetRequiredService<PayMaestroDbContext>();
    db.Database.EnsureCreated(); // demo-friendly; migrations in production
}

app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PayMaestro API v1"));

app.MapControllers();

app.Run();
