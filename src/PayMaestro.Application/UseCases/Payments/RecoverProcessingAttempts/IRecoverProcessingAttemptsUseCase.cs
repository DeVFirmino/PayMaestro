namespace PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;

public interface IRecoverProcessingAttemptsUseCase
{
    public Task<int> Execute(DateTime cutoff, int take, CancellationToken cancellationToken = default);
}
