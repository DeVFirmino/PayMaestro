using PayMaestro.API.Filters;
using PayMaestro.Application.Options;
using PayMaestro.Application.Services;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Infrastructure.DataAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>());
builder.Services.Configure<GatewayRoutingOptions>(builder.Configuration.GetSection("GatewayRouting"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
app.UseSwaggerUI();
app.MapControllers();

app.Run();