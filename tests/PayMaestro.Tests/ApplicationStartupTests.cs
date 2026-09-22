using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>A misconfigured deployment has to fail at startup, not on the first card it fingerprints.</summary>
public sealed class ApplicationStartupTests
{
    [Fact]
    public void ShouldRefuseToStartWhenFingerprintKeyIsMissingOutsideDevelopment()
    {
        using PaymentApiFactory factory = new();

        Assert.Throws<OptionsValidationException>(
            () => factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production)).CreateClient());
    }

    [Fact]
    public void ShouldRefuseToStartWhenDevelopmentFingerprintKeyIsUsedOutsideDevelopment()
    {
        using PaymentApiFactory factory = new();

        Assert.Throws<OptionsValidationException>(
            () => factory.WithWebHostBuilder(builder => builder
                    .UseEnvironment(Environments.Production)
                    .UseSetting("CardFingerprint:Key", "development-only-card-fingerprint-key-not-a-secret"))
                .CreateClient());
    }
}
