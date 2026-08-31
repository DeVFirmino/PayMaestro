using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Infrastructure.Data.Repositories;

public sealed class PaymentRepository : IPaymentReadOnlyRepository, IPaymentWriteOnlyRepository, IPaymentUpdateOnlyRepository
{
    private readonly PayMaestroDbContext _context;

    public PaymentRepository(PayMaestroDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByMerchantAndIdAsync(
        string merchantId,
        Guid id,
        CancellationToken cancellationToken)
        => await _context.Payment
            .Include(payment => payment.Attempts)
            .Include(payment => payment.FraudFlags)
            .FirstOrDefaultAsync(payment => payment.MerchantId == merchantId && payment.Id == id, cancellationToken);

    public async Task<Payment?> GetByMerchantAndIdempotencyKeyAsync(
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _context.Payment
            .Include(payment => payment.Attempts)
            .Include(payment => payment.FraudFlags)
            .FirstOrDefaultAsync(payment => payment.MerchantId == merchantId
                && payment.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListWithStaleProcessingAttemptsAsync(
        DateTime cutoff,
        int take,
        CancellationToken cancellationToken)
        => await _context.Payment
            .Include(payment => payment.Attempts)
            .Where(payment => payment.Status == PaymentStatus.Processing
                && payment.Attempts.Any(attempt => attempt.Status == PaymentAttemptStatus.Processing
                    && attempt.CreatedAt <= cutoff))
            .OrderBy(payment => payment.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        DateTime since,
        CancellationToken cancellationToken)
    {
        return await _context.Payment
            .Where(payment => payment.CardBin == cardBin && payment.CardLast4 == cardLast4)
            .SelectMany(payment => payment.Attempts)
            .CountAsync(attempt => attempt.CreatedAt >= since
                             && (attempt.ResultType == GatewayResultType.HardDecline
                                 || attempt.ResultType == GatewayResultType.SoftDecline),
                cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
        => await _context.Payment.AddAsync(payment, cancellationToken);

    public void Update(Payment payment) => _context.Payment.Update(payment);
}
