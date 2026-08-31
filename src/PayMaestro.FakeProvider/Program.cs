using PayMaestro.FakeProvider.Ledger;

namespace PayMaestro.FakeProvider;

/// <summary>
/// The fake acquirer host. It is named and namespaced on purpose: a test host locates the
/// entry point through this type, and the orchestrator's own entry point keeps its own name.
/// </summary>
public sealed class FakeProviderProgram
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSingleton<ChargeLedger>();
        builder.Services.AddHealthChecks();

        WebApplication app = builder.Build();

        app.MapHealthChecks("/health");
        app.MapControllers();

        app.Run();
    }
}
