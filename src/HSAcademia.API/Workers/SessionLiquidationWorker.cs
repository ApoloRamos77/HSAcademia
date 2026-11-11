using HSAcademia.Infrastructure.Services;

namespace HSAcademia.API.Workers;

/// <summary>
/// Cada hora revisa si toca liquidar las sesiones del mes anterior (desde el día 3, una vez por academia).
/// Después, administración puede volver a liquidar a mano si corrige asistencias.
/// </summary>
public sealed class SessionLiquidationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SessionLiquidationWorker> _logger;

    public SessionLiquidationWorker(IServiceScopeFactory scopes, ILogger<SessionLiquidationWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var done = await scope.ServiceProvider.GetRequiredService<SessionBillingService>()
                    .RunAutomaticAsync(AcademyClock.Now, stoppingToken);
                if (done > 0) _logger.LogInformation("Sesiones liquidadas automáticamente en {Count} academia(s).", done);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló la liquidación automática de sesiones.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }
}
