using Microsoft.Extensions.Options;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.Options;
using PayMaestro.Application.Services;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Domain.ValueObjects;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

/// <summary>
/// Coordinates the full payment flow, in order:
/// idempotency replay -> key reservation -> fraud screening -> gateway routing -> cascade -> persistence.
/// The reservation is committed <em>before</em> any gateway is contacted: a duplicate request
/// then loses the insert race while the money movement is still ahead of it, not behind it.
/// </summary>
public sealed class CreatePaymentUseCase : ICreatePaymentUseCase
{
    private readonly IPaymentReadOnlyRepository _readRepository;
    private readonly IPaymentWriteOnlyRepository _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IEnumerable<IFraudRule> _fraudRules;
    private readonly IOptions<GatewayRoutingOptions> _routingOptions;
    private readonly GatewayCascade _cascade;
    private readonly IPaymentRequestFingerprintGenerator _fingerprintGenerator;

    public CreatePaymentUseCase(
        IPaymentReadOnlyRepository readRepository,
        IPaymentWriteOnlyRepository writeRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways,
        IEnumerable<IFraudRule> fraudRules,
        IOptions<GatewayRoutingOptions> routingOptions,
        GatewayCascade cascade,
        IPaymentRequestFingerprintGenerator fingerprintGenerator)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
        _fraudRules = fraudRules;
        _routingOptions = routingOptions;
        _cascade = cascade;
        _fingerprintGenerator = fingerprintGenerator;
    }

    public async Task<PaymentResponse> Execute(
        string merchantId,
        string idempotencyKey,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        IdempotencyKey validatedKey = IdempotencyKey.Create(idempotencyKey);
        PaymentRequestFingerprint fingerprint = _fingerprintGenerator.Generate(merchantId, request);

        Payment? existing = await _readRepository.GetByMerchantAndIdempotencyKeyAsync(
            merchantId,
            validatedKey.Value,
            cancellationToken);
        if (existing is not null)
        {
            return HandleExisting(existing, fingerprint.RequestFingerprint);
        }

        Payment payment = CreatePaymentFrom(merchantId, validatedKey, fingerprint, request);
        payment.BeginProcessing();

        try
        {
            await _writeRepository.AddAsync(payment, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken); // claims the key; no gateway has been contacted yet
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent request claimed the key first. It owns the charge, and this request
            // has not called a gateway, so there is nothing to undo — report the winner's state.
            Payment? winner = await _readRepository.GetByMerchantAndIdempotencyKeyAsync(
                merchantId,
                validatedKey.Value,
                cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return HandleExisting(winner, fingerprint.RequestFingerprint);
        }

        if (await ScreenForFraud(payment, cancellationToken))
        {
            payment.RejectAsFraud(); // no gateway is ever contacted
        }
        else
        {
            await _cascade.ExecuteAsync(payment, ResolveRoute(payment), cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken); // guarded by the payment's concurrency stamp

        return payment.ToResponse();
    }

    private static PaymentResponse HandleExisting(Payment existing, string requestFingerprint)
    {
        if (existing.RequestFingerprint != requestFingerprint)
        {
            throw new IdempotencyKeyReuseException(existing.IdempotencyKey);
        }

        // Processing means another request is mid-charge: answering it with a fresh gateway call
        // is precisely the double charge this flow exists to prevent.
        if (existing.Status is PaymentStatus.Processing)
        {
            throw new PaymentInProgressException(existing.IdempotencyKey);
        }

        // Settled — or waiting for reconciliation, which the caller can see in the status.
        return existing.ToResponse();
    }

    /// <summary>Runs every registered fraud rule; each hit is recorded as a FraudFlag.</summary>
    private async Task<bool> ScreenForFraud(Payment payment, CancellationToken cancellationToken)
    {
        bool suspicious = false;

        foreach (IFraudRule rule in _fraudRules)
        {
            FraudVerdict verdict = await rule.EvaluateAsync(payment, cancellationToken);
            if (verdict.IsSuspicious is false)
            {
                continue;
            }

            payment.AddFraudFlag(FraudFlag.Create(payment.Id, rule.RuleName, verdict.Details ?? ""));
            suspicious = true;
        }

        return suspicious;
    }

    private static Payment CreatePaymentFrom(
        string merchantId,
        IdempotencyKey idempotencyKey,
        PaymentRequestFingerprint fingerprint,
        CreatePaymentRequest request)
        => Payment.Create(
            merchantId, idempotencyKey, fingerprint.RequestFingerprint,
            request.MerchantReference, request.CustomerId,
            request.Amount, request.Currency.ToUpperInvariant(),
            cardBin: request.CardNumber[..6],
            cardLast4: request.CardNumber[^4..],
            paymentMethodToken: fingerprint.PaymentMethodToken,
            // country lookups stubbed; production would resolve them via BIN table / GeoIP
            cardCountry: "MT", customerIp: request.CustomerIp, ipCountry: "MT");

    private List<IPaymentGateway> ResolveRoute(Payment payment)
        => _routingOptions.Value.Gateways
            .Where(configuration => configuration.SupportedCurrencies.Contains(payment.Currency)
                       && payment.Amount <= configuration.MaxAmount)
            .OrderBy(configuration => configuration.Priority)
            .Select(configuration => _gateways.FirstOrDefault(gateway => gateway.Name == configuration.Name))
            .OfType<IPaymentGateway>()
            .ToList();
}
