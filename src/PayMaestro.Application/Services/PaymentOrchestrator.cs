using Microsoft.Extensions.Options;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Exceptions;
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
    IOptions<GatewayRoutingOptions> routingOptions,
    CascadeExecutor cascade)
{
    // Velocity rule: a card declined this many times within the window is
    // treated as card testing / stolen-card probing and rejected upfront.
    private const int MaxRecentDeclines = 3;
    private static readonly TimeSpan DeclineWindow = TimeSpan.FromHours(24);

    public async Task<ResponsePaymentJson> CreatePayment(string idempotencyKey, RequestCreatePaymentJson request)
    {
        var existing = await readRepo.GetByIdempotencyKey(idempotencyKey);
        if (existing is not null)
            return ReplayExisting(existing, request);

        var payment = CreatePaymentFrom(idempotencyKey, request);

        if (await ExceedsDeclineVelocity(payment))
            RejectAsFraud(payment);          // no gateway is ever contacted
        else
            await cascade.ExecuteAsync(payment, ResolveRoute(payment));

        await writeRepo.Add(payment);        // fraud-rejected payments are stored too (audit trail)
        await unitOfWork.Commit();

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

    private async Task<bool> ExceedsDeclineVelocity(Payment payment)
        => await readRepo.CountRecentDeclinedAttempts(payment.CardBin, payment.CardLast4, DeclineWindow)
           >= MaxRecentDeclines;

    private static void RejectAsFraud(Payment payment)
    {
        payment.AddFraudFlag(FraudFlag.Create(
            payment.Id,
            ruleName: "DeclineVelocity",
            details: $"Card reached {MaxRecentDeclines} declined attempts within {DeclineWindow.TotalHours}h."));

        payment.RejectAsFraud();
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