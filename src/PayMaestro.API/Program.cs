using PayMaestro.API;
using PayMaestro.API.Filters;
using PayMaestro.Application;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>());
builder.Services.AddApiDocumentation();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Local runs create the schema from the model. Anywhere else the migrations are applied
    // by a deployment step, never by the application.
    app.Services.EnsureDatabaseCreated();
}

app.UseApiDocumentation();
app.MapControllers();

app.Run();
