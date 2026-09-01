using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Infrastructure.Data.Repositories;

public sealed class PaymentRepository : IPaymentReadOnlyRepository, IPaymentWriteOnlyRepository
{
    private readonly PayMaestroDbContext _context;

    public PaymentRepository(PayMaestroDbContext context)
    {
        _context = context;
    }

    /// <summary>A payment is always loaded with its audit history: attempts and fraud flags.</summary>
    private IQueryable<Payment> PaymentsWithHistory => _context.Payments
        .Include(payment => payment.Attempts)
        .Include(payment => payment.FraudFlags);

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken)
        => PaymentsWithHistory.FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        => PaymentsWithHistory.FirstOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        DateTime cutoff = DateTime.UtcNow - window;

        return _context.Payments
            .Where(payment => payment.CardBin == cardBin && payment.CardLast4 == cardLast4)
            .SelectMany(payment => payment.Attempts)
            .CountAsync(
                attempt => attempt.CreatedAt >= cutoff
                           && (attempt.ResultType == GatewayResultType.HardDecline
                               || attempt.ResultType == GatewayResultType.SoftDecline),
                cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
        => await _context.Payments.AddAsync(payment, cancellationToken);
}
