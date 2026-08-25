using Microsoft.Extensions.Options;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.Services;

/// <summary>
/// Coordinates the full payment flow, in order:
/// idempotency replay -> key reservation -> fraud screening -> gateway routing -> cascade -> persistence.
/// The reservation is committed <em>before</em> any gateway is contacted: a duplicate request
/// then loses the insert race while the money movement is still ahead of it, not behind it.
/// </summary>
public class PaymentOrchestrator(
    IPaymentReadOnlyRepository readRepo,
    IPaymentWriteOnlyRepository writeRepo,
    IUnitOfWork unitOfWork,
    IEnumerable<IPaymentGateway> gateways,
    IEnumerable<IFraudRule> fraudRules,
    IOptions<GatewayRoutingOptions> routingOptions,
    CascadeExecutor cascade)
{
    public async Task<ResponsePaymentJson> CreatePayment(
        string idempotencyKey, RequestCreatePaymentJson request, CancellationToken ct = default)
    {
        var existing = await readRepo.GetByIdempotencyKey(idempotencyKey);
        if (existing is not null)
            return HandleExisting(existing, request);

        var payment = CreatePaymentFrom(idempotencyKey, request);
        payment.BeginProcessing();

        try
        {
            await writeRepo.Add(payment);
            await unitOfWork.Commit();       // claims the key; no gateway has been contacted yet
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent request claimed the key first. It owns the charge, and this request
            // has not called a gateway, so there is nothing to undo — report the winner's state.
            var winner = await readRepo.GetByIdempotencyKey(idempotencyKey);
            if (winner is null) throw;
            return HandleExisting(winner, request);
        }

        if (await ScreenForFraud(payment))
            payment.RejectAsFraud();         // no gateway is ever contacted
        else
            await cascade.ExecuteAsync(payment, ResolveRoute(payment), ct);

        await unitOfWork.Commit();           // guarded by the payment's concurrency stamp

        return payment.ToResponse();
    }

    public async Task<ResponsePaymentJson?> GetById(Guid id)
    {
        var payment = await readRepo.GetById(id);
        return payment?.ToResponse();
    }

    private static ResponsePaymentJson HandleExisting(Payment existing, RequestCreatePaymentJson request)
    {
        if (existing.Amount != request.Amount || existing.MerchantReference != request.MerchantReference)
            throw new IdempotencyKeyReuseException(existing.IdempotencyKey);

        // Processing means another request is mid-charge: answering it with a fresh gateway call
        // is precisely the double charge this flow exists to prevent.
        if (existing.Status is PaymentStatus.Processing)
            throw new PaymentInProgressException(existing.IdempotencyKey);

        // Settled — or waiting for reconciliation, which the caller can see in the status.
        return existing.ToResponse();
    }

    /// <summary>Runs every registered fraud rule; each hit is recorded as a FraudFlag.</summary>
    private async Task<bool> ScreenForFraud(Payment payment)
    {
        var suspicious = false;

        foreach (var rule in fraudRules)
        {
            var verdict = await rule.EvaluateAsync(payment);
            if (!verdict.IsSuspicious)
                continue;

            payment.AddFraudFlag(FraudFlag.Create(payment.Id, rule.RuleName, verdict.Details ?? ""));
            suspicious = true;
        }

        return suspicious;
    }

    private static Payment CreatePaymentFrom(string idempotencyKey, RequestCreatePaymentJson request)
        => Payment.Create(
            idempotencyKey, request.MerchantReference, request.CustomerId,
            request.Amount, request.Currency.ToUpperInvariant(),
            cardBin: request.CardNumber[..6],
            cardLast4: request.CardNumber[^4..],
            // country lookups stubbed; production would resolve them via BIN table / GeoIP
            cardCountry: "MT", customerIp: request.CustomerIp, ipCountry: "MT");

    private List<IPaymentGateway> ResolveRoute(Payment payment)
        => routingOptions.Value.Gateways
            .Where(cfg => cfg.SupportedCurrencies.Contains(payment.Currency)
                       && payment.Amount <= cfg.MaxAmount)
            .OrderBy(cfg => cfg.Priority)
            .Select(cfg => gateways.FirstOrDefault(g => g.Name == cfg.Name))
            .OfType<IPaymentGateway>()
            .ToList();
}
