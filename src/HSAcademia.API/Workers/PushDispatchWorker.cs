using HSAcademia.Infrastructure.Services.Push;

namespace HSAcademia.API.Workers;

/// <summary>Cada pocos segundos envía las notificaciones pendientes.</summary>
public sealed class PushDispatchWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PushDispatchWorker> _logger;

    public PushDispatchWorker(IServiceScopeFactory scopes, ILogger<PushDispatchWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PushDispatchService>().DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Lo no enviado sigue pendiente y se reintenta en el siguiente ciclo.
                _logger.LogError(ex, "Falló el envío de notificaciones.");
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }
}
