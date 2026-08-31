using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.Services;

namespace PayMaestro.Infrastructure.PaymentRequests;

/// <summary>
/// Turns a payment request into two keyed hashes: a token that stands for the card, and a
/// fingerprint of everything the request asks the platform to do.
/// <para>
/// The fingerprint covers every field that changes what is charged, the payment method
/// included. Two different card numbers that share a BIN and the last four digits produce two
/// different tokens, so they produce two different fingerprints. The card number itself is
/// never stored: only the keyed hash of it is.
/// </para>
/// </summary>
public sealed class HmacPaymentRequestFingerprintGenerator : IPaymentRequestFingerprintGenerator
{
    /// <summary>The configuration key that holds the HMAC secret.</summary>
    public const string SecretConfigurationKey = "PaymentSecurity:FingerprintSecret";

    private readonly byte[] _key;

    public HmacPaymentRequestFingerprintGenerator(IConfiguration configuration)
    {
        string? secret = configuration[SecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"Configuration '{SecretConfigurationKey}' is required. It keys the request fingerprint.");
        }

        _key = Encoding.UTF8.GetBytes(secret);
    }

    public PaymentRequestFingerprint Generate(string merchantId, CreatePaymentRequest request)
    {
        string paymentMethodToken = Hmac($"payment-method|{merchantId}|{NormalizeCard(request.CardNumber)}");

        SortedDictionary<string, string> canonical = new(StringComparer.Ordinal)
        {
            ["amount"] = request.Amount.ToString("0.00################", CultureInfo.InvariantCulture),
            ["currency"] = request.Currency.Trim().ToUpperInvariant(),
            ["customerId"] = request.CustomerId.Trim(),
            ["customerIp"] = request.CustomerIp.Trim(),
            ["merchantId"] = merchantId.Trim(),
            ["merchantReference"] = request.MerchantReference.Trim(),
            ["paymentMethodToken"] = paymentMethodToken
        };

        string json = JsonSerializer.Serialize(canonical);

        return new PaymentRequestFingerprint
        {
            PaymentMethodToken = paymentMethodToken,
            RequestFingerprint = Hmac(json)
        };
    }

    private string Hmac(string value)
    {
        using HMACSHA256 hmac = new(_key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeCard(string cardNumber)
        => new(cardNumber.Where(char.IsDigit).ToArray());
}
