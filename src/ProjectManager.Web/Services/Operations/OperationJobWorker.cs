using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.Operations;

public sealed class OperationJobWorker(
    IServiceScopeFactory scopeFactory,
    OperationWorkerHeartbeat heartbeat,
    ILogger<OperationJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeat.Beat();
            OperationJob? job = null;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<OperationJobService>();
                job = await jobs.ClaimNextAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var dispatcher = scope.ServiceProvider.GetRequiredService<OperationHandlerDispatcher>();
                await dispatcher.ExecuteAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Operation job {JobId} failed.", job?.Id);
                if (job is not null)
                {
                    try
                    {
                        using var failureScope = scopeFactory.CreateScope();
                        var jobs = failureScope.ServiceProvider.GetRequiredService<OperationJobService>();
                        await jobs.FailAsync(job.Id, exception.ToString(), stoppingToken);
                    }
                    catch (Exception failureException)
                    {
                        logger.LogError(failureException, "Unable to persist failure for operation job {JobId}.", job.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<OperationJobService>();
        var recovered = await jobs.RecoverAbandonedAsync(TimeSpan.FromMinutes(10), cancellationToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} abandoned operation jobs.", recovered);
        }
    }
}
