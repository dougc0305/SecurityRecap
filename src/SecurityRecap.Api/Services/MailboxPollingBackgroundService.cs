using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Services;

/// <summary>
/// Wakes on a fixed tick and polls every property whose mailbox poll interval has elapsed.
/// The per-property cadence lives in the database; this only decides how often to look.
/// </summary>
public class MailboxPollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MailboxPollingBackgroundService> _logger;

    public MailboxPollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<MailboxPollingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    private TimeSpan TickInterval
    {
        get
        {
            var seconds = _config.GetValue<int?>("MailboxIngest:TickIntervalSeconds") ?? 60;
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 15, 3600));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!(_config.GetValue<bool?>("MailboxIngest:Enabled") ?? true))
        {
            _logger.LogInformation("Mailbox polling is disabled by configuration; background service will not run");
            return;
        }

        _logger.LogInformation("Mailbox polling started with a {Interval} tick", TickInterval);

        // Give the app a moment to finish starting (migrations, seeding) before the first pass.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollDuePropertiesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad tick kill the loop; the next tick retries.
                _logger.LogError(ex, "Mailbox polling tick failed");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Mailbox polling stopped");
    }

    private async Task PollDuePropertiesAsync(CancellationToken ct)
    {
        // A fresh scope per tick so the DbContext does not accumulate tracked entities
        // across the lifetime of the process.
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMailboxIngestionService>();

        var due = await service.GetPropertiesDueForPollAsync(ct);
        if (due.Count == 0) return;

        _logger.LogInformation("{Count} propert(ies) due for a mailbox poll", due.Count);

        foreach (var propertyId in due)
        {
            ct.ThrowIfCancellationRequested();

            // Each property gets its own scope: a poll runs Claude analysis and can take
            // minutes, and one property's failure must not poison another's DbContext.
            using var propertyScope = _scopeFactory.CreateScope();
            var propertyService = propertyScope.ServiceProvider.GetRequiredService<IMailboxIngestionService>();

            try
            {
                var result = await propertyService.PollPropertyAsync(propertyId, ct);

                if (!result.Succeeded)
                    _logger.LogWarning("Poll for {PropertyName} reported: {Error}", result.PropertyName, result.Error);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling property {PropertyId} threw", propertyId);
            }
        }
    }
}
