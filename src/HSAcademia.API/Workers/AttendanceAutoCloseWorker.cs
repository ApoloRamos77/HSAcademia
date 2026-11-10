using HSAcademia.Application.Interfaces;

namespace HSAcademia.API.Workers;

/// <summary>Cada hora cierra las listas de asistencia con el plazo vencido.</summary>
public sealed class AttendanceAutoCloseWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AttendanceAutoCloseWorker> _logger;

    public AttendanceAutoCloseWorker(IServiceScopeFactory scopes, ILogger<AttendanceAutoCloseWorker> logger)
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
                var closed = await scope.ServiceProvider.GetRequiredService<IAttendanceService>().AutoCloseExpiredAsync();
                if (closed > 0) _logger.LogInformation("Cierre automático de asistencia: {Count} sesión(es).", closed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló el cierre automático de asistencia.");
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
