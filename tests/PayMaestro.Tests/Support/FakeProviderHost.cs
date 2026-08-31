using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using PayMaestro.FakeProvider;

namespace PayMaestro.Tests.Support;

/// <summary>
/// Runs the fake acquirer over a real HTTP pipeline, on its own ledger file. A test that uses
/// it exercises the same boundary the deployed orchestrator uses: HTTP in, HTTP out.
/// </summary>
public sealed class FakeProviderHost : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"paymaestro-provider-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<FakeProviderProgram> _factory;

    public FakeProviderHost()
    {
        _factory = new ProviderFactory(_path);
        Client = _factory.CreateClient();
    }

    public HttpClient Client { get; }

    /// <summary>Hands the same client to every named client the gateway asks for.</summary>
    public IHttpClientFactory HttpClientFactory => new SingleClientFactory(Client);

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private sealed class ProviderFactory : WebApplicationFactory<FakeProviderProgram>
    {
        private readonly string _path;

        public ProviderFactory(string path)
        {
            _path = path;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Ledger", $"Data Source={_path}");
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }
}
