using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PayMaestro.Tests.Support;

/// <summary>
/// Boots the real API — controllers, filters and API behaviour options included — against a
/// throwaway SQLite file, so a test can assert what a client actually receives over HTTP.
/// </summary>
public sealed class PaymentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"paymaestro-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_path}")
            .UseSetting("PaymentSecurity:FingerprintSecret", "test-fingerprint-secret")
            .ConfigureTestServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });
            });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools(); // release the file handle before deleting it
        File.Delete(_path);
    }

    public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        /// <summary>A request that carries this header is treated as an anonymous caller.</summary>
        public const string AnonymousHeader = "X-Test-Anonymous";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey(AnonymousHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new("merchant_id", "merchant-1"),
                new("scope", "payments:write"),
                new("scope", "payments:read"),
                new("scope", "payments:reconcile")
            ];
            ClaimsIdentity identity = new(claims, "Test");
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
