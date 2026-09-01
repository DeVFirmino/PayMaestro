using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

/// <summary>
/// Runs the full payment flow, in order:
/// idempotency replay -> key reservation -> fraud screening -> gateway routing -> cascade -> persistence.
/// The reservation is committed <em>before</em> any gateway is contacted: a duplicate request
/// then loses the insert race while the money movement is still ahead of it, not behind it.
/// </summary>
public sealed class CreatePaymentUseCase : ICreatePaymentUseCase
{
    // Country lookups are stubbed; production would resolve them via a BIN table and GeoIP.
    private const string StubCountry = "MT";

    private readonly IPaymentReadOnlyRepository _paymentsReader;
    private readonly IPaymentWriteOnlyRepository _paymentsWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IFraudRule> _fraudRules;
    private readonly GatewayRouter _router;
    private readonly CascadeExecutor _cascade;

    public CreatePaymentUseCase(
        IPaymentReadOnlyRepository paymentsReader,
        IPaymentWriteOnlyRepository paymentsWriter,
        IUnitOfWork unitOfWork,
        IEnumerable<IFraudRule> fraudRules,
        GatewayRouter router,
        CascadeExecutor cascade)
    {
        _paymentsReader = paymentsReader;
        _paymentsWriter = paymentsWriter;
        _unitOfWork = unitOfWork;
        _fraudRules = fraudRules;
        _router = router;
        _cascade = cascade;
    }

    public async Task<PaymentResponse> Execute(
        string idempotencyKey,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationFailedException(ErrorMessages.IdempotencyKeyHeaderRequired);
        }

        Payment? existing = await _paymentsReader.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ReplayExisting(existing, request);
        }

        Payment payment = CreatePaymentFrom(idempotencyKey, request);
        payment.BeginProcessing();

        try
        {
            await _paymentsWriter.AddAsync(payment, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);       // claims the key; no gateway has been contacted yet
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent request claimed the key first. It owns the charge, and this request
            // has not called a gateway, so there is nothing to undo — report the winner's state.
            Payment? winner = await _paymentsReader.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return ReplayExisting(winner, request);
        }

        if (await IsSuspiciousAsync(payment, cancellationToken))
        {
            payment.RejectAsFraud();                                // no gateway is ever contacted
        }
        else
        {
            IReadOnlyList<IPaymentGateway> route = _router.RouteFor(payment);
            await _cascade.ExecuteAsync(payment, route, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);           // guarded by the payment's concurrency stamp

        return payment.ToResponse();
    }

    private static PaymentResponse ReplayExisting(Payment existing, CreatePaymentRequest request)
    {
        if (existing.Amount != request.Amount || existing.MerchantReference != request.MerchantReference)
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
    private async Task<bool> IsSuspiciousAsync(Payment payment, CancellationToken cancellationToken)
    {
        bool suspicious = false;

        foreach (IFraudRule rule in _fraudRules)
        {
            FraudVerdict verdict = await rule.EvaluateAsync(payment, cancellationToken);
            if (verdict.IsSuspicious is false)
            {
                continue;
            }

            payment.AddFraudFlag(FraudFlag.Create(payment.Id, rule.RuleName, verdict.Details ?? string.Empty));
            suspicious = true;
        }

        return suspicious;
    }

    private static Payment CreatePaymentFrom(string idempotencyKey, CreatePaymentRequest request)
        => Payment.Create(
            idempotencyKey,
            request.MerchantReference,
            request.CustomerId,
            request.Amount,
            request.Currency,
            cardBin: request.CardNumber[..Payment.CardBinLength],
            cardLast4: request.CardNumber[^Payment.CardLast4Length..],
            cardCountry: StubCountry,
            customerIp: request.CustomerIp,
            ipCountry: StubCountry);
}
