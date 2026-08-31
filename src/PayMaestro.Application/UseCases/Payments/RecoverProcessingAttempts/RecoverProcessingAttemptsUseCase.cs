using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;

public sealed class RecoverProcessingAttemptsUseCase : IRecoverProcessingAttemptsUseCase
{
    private readonly IPaymentReadOnlyRepository _readRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public RecoverProcessingAttemptsUseCase(
        IPaymentReadOnlyRepository readRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways)
    {
        _readRepository = readRepository;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
    }

    public async Task<int> Execute(DateTime cutoff, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Payment> payments = await _readRepository.ListWithStaleProcessingAttemptsAsync(
            cutoff,
            take,
            cancellationToken);

        int recovered = 0;
        foreach (Payment payment in payments)
        {
            PaymentAttempt attempt = payment.Attempts
                .Where(candidate => candidate.Status == PaymentAttemptStatus.Processing)
                .OrderByDescending(candidate => candidate.AttemptOrder)
                .First();

            IPaymentGateway? gateway = _gateways.FirstOrDefault(candidate => candidate.Name == attempt.GatewayName);
            if (gateway is null)
            {
                continue;
            }

            GatewayResult outcome = await gateway.QueryAsync(attempt.ProviderIdempotencyKey, cancellationToken);
            attempt.Complete(outcome.ResultType, outcome.ResponseCode, attempt.DurationMs);

            switch (outcome.ResultType)
            {
                case GatewayResultType.Approved:
                    payment.Authorize();
                    payment.Capture();
                    break;

                case GatewayResultType.Uncertain:
                    payment.MarkForReconciliation();
                    break;

                default:
                    payment.Decline();
                    break;
            }

            recovered++;
        }

        if (recovered > 0)
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        return recovered;
    }
}
