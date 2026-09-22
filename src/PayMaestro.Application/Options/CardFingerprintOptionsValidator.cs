using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PayMaestro.Application.Options;

/// <summary>
/// Runs at startup. Outside Development the application refuses to start without a real key,
/// instead of fingerprinting cards under an empty or published one.
/// </summary>
public sealed class CardFingerprintOptionsValidator : IValidateOptions<CardFingerprintOptions>
{
    private readonly IHostEnvironment _environment;

    public CardFingerprintOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, CardFingerprintOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            return ValidateOptionsResult.Fail(
                $"{CardFingerprintOptions.SectionName}:Key is not set. Set it through the CardFingerprint__Key environment variable.");
        }

        if (options.Key.Length < CardFingerprintOptions.MinimumKeyLength)
        {
            return ValidateOptionsResult.Fail(
                $"{CardFingerprintOptions.SectionName}:Key must be at least {CardFingerprintOptions.MinimumKeyLength} characters long.");
        }

        if (_environment.IsDevelopment() is false
            && options.Key.StartsWith(CardFingerprintOptions.DevelopmentKeyPrefix, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{CardFingerprintOptions.SectionName}:Key is the development key, which is refused outside Development.");
        }

        return ValidateOptionsResult.Success;
    }
}
