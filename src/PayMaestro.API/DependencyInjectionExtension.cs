using Microsoft.OpenApi;

namespace PayMaestro.API;

public static class DependencyInjectionExtension
{
    private const string DocumentName = "v1";

    public static void AddApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "PayMaestro API",
                Version = DocumentName,
                Description = "Payment orchestration API: multi-gateway cascade with automatic failover, "
                            + "completed-outcome replay and fraud velocity screening.",
            });

            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PayMaestro.API.xml"));
        });
    }

    public static void UseApiDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
            options.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", "PayMaestro API v1"));
    }
}
