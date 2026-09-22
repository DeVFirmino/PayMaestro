using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PayMaestro.Application.Options;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The committed development key is public by definition. These pin the rule that keeps it, or no
/// key at all, from ever fingerprinting cards outside Development.
/// </summary>
public sealed class CardFingerprintOptionsValidatorTests
{
    private const string DevelopmentKey = "development-only-card-fingerprint-key-not-a-secret";
    private const string ProductionLikeKey = "a-key-that-only-the-deployment-holds-0123";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldFailWhenKeyIsMissingOutsideDevelopment(string key)
    {
        ValidateOptionsResult result = Validate(Environments.Production, key);

        Assert.True(result.Failed);
    }

    [Fact]
    public void ShouldFailWhenDevelopmentKeyIsUsedOutsideDevelopment()
    {
        ValidateOptionsResult result = Validate(Environments.Production, DevelopmentKey);

        Assert.True(result.Failed);
    }

    [Fact]
    public void ShouldFailWhenKeyIsShorterThanMinimum()
    {
        ValidateOptionsResult result = Validate(Environments.Production, "too-short");

        Assert.True(result.Failed);
    }

    [Fact]
    public void ShouldSucceedWhenDevelopmentKeyIsUsedInDevelopment()
    {
        ValidateOptionsResult result = Validate(Environments.Development, DevelopmentKey);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ShouldSucceedWhenRealKeyIsSetOutsideDevelopment()
    {
        ValidateOptionsResult result = Validate(Environments.Production, ProductionLikeKey);

        Assert.True(result.Succeeded);
    }

    private static ValidateOptionsResult Validate(string environmentName, string key)
        => new CardFingerprintOptionsValidator(new StubHostEnvironment(environmentName))
            .Validate(Options.DefaultName, new CardFingerprintOptions { Key = key });
}
