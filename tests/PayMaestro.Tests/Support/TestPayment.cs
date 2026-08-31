using PayMaestro.Domain.Entities;
using PayMaestro.Domain.ValueObjects;

namespace PayMaestro.Tests.Support;

/// <summary>Builds a payment with sensible values, so a test names only what it cares about.</summary>
public static class TestPayment
{
    public static Payment New(
        string merchantId = "merchant-1",
        string idempotencyKey = "key-1",
        string requestFingerprint = "fingerprint-1",
        string merchantReference = "ORDER-1",
        string customerId = "cust-1",
        decimal amount = 100m,
        string currency = "EUR",
        string cardBin = "411111",
        string cardLast4 = "7777",
        string paymentMethodToken = "payment-method-token",
        string cardCountry = "MT",
        string customerIp = "203.0.113.10",
        string ipCountry = "MT")
        => Payment.Create(
            merchantId, IdempotencyKey.Create(idempotencyKey), requestFingerprint, merchantReference, customerId,
            amount, currency, cardBin, cardLast4, paymentMethodToken,
            cardCountry, customerIp, ipCountry);

    /// <summary>A payment whose key is already claimed, which is the only state a cascade runs on.</summary>
    public static Payment Reserved(string cardLast4 = "7777", string idempotencyKey = "key-1")
    {
        Payment payment = New(cardLast4: cardLast4, idempotencyKey: idempotencyKey);
        payment.BeginProcessing();

        return payment;
    }
}
