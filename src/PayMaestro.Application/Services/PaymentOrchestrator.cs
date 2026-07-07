using Microsoft.Extensions.Options;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.Services;

/// <summary>
/// Coordinates the full payment flow, in order:
/// idempotency replay -> fraud screening -> gateway routing -> cascade -> persistence.
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
    public async Task<ResponsePaymentJson> CreatePayment(string idempotencyKey, RequestCreatePaymentJson request)
    {
        var existing = await readRepo.GetByIdempotencyKey(idempotencyKey);
        if (existing is not null)
            return ReplayExisting(existing, request);

        var payment = CreatePaymentFrom(idempotencyKey, request);

        if (await ScreenForFraud(payment))
            payment.RejectAsFraud();         // no gateway is ever contacted
        else
            await cascade.ExecuteAsync(payment, ResolveRoute(payment));

        try
        {
            await writeRepo.Add(payment);    // fraud-rejected payments are stored too (audit trail)
            await unitOfWork.Commit();
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent request with the same idempotency key won the race:
            // the unique index serialized us, so replay the winner's outcome.
            var winner = await readRepo.GetByIdempotencyKey(idempotencyKey);
            if (winner is null) throw;
            return ReplayExisting(winner, request);
        }

        return ToResponse(payment);
    }

    public async Task<ResponsePaymentJson?> GetById(Guid id)
    {
        var payment = await readRepo.GetById(id);
        return payment is null ? null : ToResponse(payment);
    }

    private ResponsePaymentJson ReplayExisting(Payment existing, RequestCreatePaymentJson request)
    {
        if (existing.Amount != request.Amount || existing.MerchantReference != request.MerchantReference)
            throw new IdempotencyKeyReuseException(existing.IdempotencyKey);

        return ToResponse(existing);         // replay: no double charge
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

    private static ResponsePaymentJson ToResponse(Payment p) => new(
        p.Id, p.MerchantReference, p.Amount, p.Currency, p.CardLast4,
        p.Status.ToString(), p.CreatedAt,
        p.Attempts.OrderBy(a => a.AttemptOrder)
            .Select(a => new ResponseAttemptJson(a.GatewayName, a.AttemptOrder,
                a.ResultType.ToString(), a.GatewayResponseCode, a.DurationMs))
            .ToList());
}