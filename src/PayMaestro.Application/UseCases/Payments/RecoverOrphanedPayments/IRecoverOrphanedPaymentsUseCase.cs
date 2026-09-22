using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.RecoverOrphanedPayments;

public interface IRecoverOrphanedPaymentsUseCase
{
    Task<RecoverOrphanedPaymentsResponse> Execute(CancellationToken cancellationToken);
}
