using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DA.NA.Staleness;

/// <summary>
/// Runs freshness reminder checks on a configurable interval (default: daily).
/// </summary>
public class FreshnessReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FreshnessReminderBackgroundService> _logger;

    public FreshnessReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<FreshnessReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("FreshnessReminders:Enabled", true))
        {
            _logger.LogInformation("Freshness reminder background service is disabled.");
            return;
        }

        var intervalHours = _configuration.GetValue("FreshnessReminders:IntervalHours", 24);
        var interval = TimeSpan.FromHours(intervalHours);
        _logger.LogInformation(
            "Freshness reminder background service started (interval: {IntervalHours}h).",
            intervalHours);

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Freshness reminder job failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FreshnessReminderService>();
        await service.ProcessDueRemindersAsync(cancellationToken);
    }
}
