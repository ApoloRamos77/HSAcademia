using HSAcademia.Infrastructure.Services;

namespace HSAcademia.API.Workers;

/// <summary>Cada 5 minutos crea los avisos de encuentros y cumpleaños que tocan; el despachador de notificaciones los envía.</summary>
public sealed class ScheduledNoticeWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ScheduledNoticeWorker> _logger;

    public ScheduledNoticeWorker(IServiceScopeFactory scopes, ILogger<ScheduledNoticeWorker> logger)
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
                var created = await scope.ServiceProvider.GetRequiredService<ScheduledNoticeService>()
                    .CreateDueAsync(AcademyClock.Now, stoppingToken);
                if (created > 0) _logger.LogInformation("Avisos de encuentros y cumpleaños creados: {Count}.", created);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló la creación de avisos de encuentros y cumpleaños.");
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
