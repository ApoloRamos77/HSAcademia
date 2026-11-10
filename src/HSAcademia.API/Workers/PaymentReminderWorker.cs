using HSAcademia.Infrastructure.Services;

namespace HSAcademia.API.Workers;

/// <summary>Cada 15 minutos crea los recordatorios de pago que tocan; el despachador de notificaciones los envía.</summary>
public sealed class PaymentReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PaymentReminderWorker> _logger;

    public PaymentReminderWorker(IServiceScopeFactory scopes, ILogger<PaymentReminderWorker> logger)
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
                var created = await scope.ServiceProvider.GetRequiredService<PaymentReminderService>()
                    .CreateDueRemindersAsync(AcademyClock.Now, stoppingToken);
                if (created > 0) _logger.LogInformation("Recordatorios de pago creados: {Count}.", created);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló la creación de recordatorios de pago.");
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
