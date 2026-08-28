using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace PayMaestro.Tests.Support;

/// <summary>
/// Boots the real API — controllers, filters and API behaviour options included — against a
/// throwaway SQLite file, so a test can assert what a client actually receives over HTTP.
/// </summary>
public sealed class PaymentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"paymaestro-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_path}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools(); // release the file handle before deleting it
        File.Delete(_path);
    }
}
