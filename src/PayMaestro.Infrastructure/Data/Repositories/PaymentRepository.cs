using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Repositories.PaymentRepository;
using PayMaestro.Infrastructure.Data;

namespace PayMaestro.Infrastructure.Data.Repositories;

public class PaymentRepository(PayMaestroDbContext context) :
    IPaymentReadOnlyRepository, IPaymentWriteOnlyRepository, IPaymentUpdateOnlyRepository
{
    public async Task<Payment?> GetById(Guid id)
        => await context.Payment
            .Include(p => p.Attempts)
            .Include(p => p.FraudFlags)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Payment?> GetByIdempotencyKey(string idempotencyKey)
        => await context.Payment
            .Include(p => p.Attempts)
            .Include(p => p.FraudFlags)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);

    public async Task<int> CountRecentDeclinedAttempts(string cardBin, string cardLast4, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return await context.Payment
            .Where(p => p.CardBin == cardBin && p.CardLast4 == cardLast4)
            .SelectMany(p => p.Attempts)
            .CountAsync(a => a.CreatedAt >= cutoff
                             && (a.ResultType == GatewayResultType.HardDecline
                                 || a.ResultType == GatewayResultType.SoftDecline));
    }

    public async Task Add(Payment payment) => await context.Payment.AddAsync(payment);

    public void Update(Payment payment) => context.Payment.Update(payment);
}