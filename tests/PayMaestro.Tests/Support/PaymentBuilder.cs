using PayMaestro.Domain.Entities;

namespace PayMaestro.Tests.Support;

/// <summary>A valid payment with load-bearing defaults every suite shares: key "key-1", 100 EUR, card 411111…7777.</summary>
public sealed class PaymentBuilder
{
    private decimal _amount = 100m;

    public PaymentBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public Payment Build() => Payment.Create(
        idempotencyKey: "key-1",
        merchantReference: "ORDER-1",
        customerId: "cust-1",
        amount: _amount,
        currency: "EUR",
        cardBin: "411111",
        cardLast4: "7777",
        cardCountry: "MT",
        customerIp: "203.0.113.10",
        ipCountry: "MT");

    /// <summary>A payment whose key is already reserved: the only state the cascade ever runs on.</summary>
    public Payment BuildReserved()
    {
        Payment payment = Build();
        payment.BeginProcessing();

        return payment;
    }
}
