using PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;

namespace PayMaestro.API.Workers;

public sealed class PaymentAttemptRecoveryWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentAttemptRecoveryWorker> _logger;

    public PaymentAttemptRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<PaymentAttemptRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IRecoverProcessingAttemptsUseCase useCase =
                    scope.ServiceProvider.GetRequiredService<IRecoverProcessingAttemptsUseCase>();

                int recovered = await useCase.Execute(
                    _timeProvider.GetUtcNow().UtcDateTime - StaleAfter,
                    take: 25,
                    stoppingToken);

                if (recovered > 0)
                {
                    _logger.LogInformation("Recovered {RecoveredAttempts} stale payment attempts.", recovered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // This loop is the boundary of the host: an escaping exception would stop the
                // whole application. Whatever failed, the pass ends here and the next one
                // starts from a fresh scope and re-reads whatever is still stale.
                _logger.LogError(exception, "Payment attempt recovery failed.");
            }
        }
    }
}
