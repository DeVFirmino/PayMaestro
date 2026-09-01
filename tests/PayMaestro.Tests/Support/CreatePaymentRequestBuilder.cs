using PayMaestro.Application.Contracts;

namespace PayMaestro.Tests.Support;

public sealed class CreatePaymentRequestBuilder
{
    private decimal _amount = 100m;
    private string _cardNumber = "4111111111117777";

    public CreatePaymentRequestBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public CreatePaymentRequestBuilder WithCardNumber(string cardNumber)
    {
        _cardNumber = cardNumber;
        return this;
    }

    public CreatePaymentRequest Build() => new()
    {
        MerchantReference = "ORDER-1",
        CustomerId = "cust-1",
        Amount = _amount,
        Currency = "EUR",
        CardNumber = _cardNumber,
        CustomerIp = "203.0.113.10",
    };
}
