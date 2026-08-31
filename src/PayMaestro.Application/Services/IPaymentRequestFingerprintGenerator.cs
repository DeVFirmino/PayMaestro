using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.Services;

public interface IPaymentRequestFingerprintGenerator
{
    public PaymentRequestFingerprint Generate(string merchantId, CreatePaymentRequest request);
}
