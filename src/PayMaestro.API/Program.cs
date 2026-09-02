using PayMaestro.API;
using PayMaestro.API.Filters;
using PayMaestro.Application;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>())
    // An empty status result must reach the client as-is: GET of an unknown payment answers
    // 404 with an empty body, not a synthesized ProblemDetails envelope.
    .ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true);
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

public partial class Program { } // exposes the entry point to WebApplicationFactory in the tests
