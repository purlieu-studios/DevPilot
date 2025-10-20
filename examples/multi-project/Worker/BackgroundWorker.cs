using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker;

/// <summary>
/// Background worker for async processing tasks.
/// </summary>
public class BackgroundWorker : BackgroundService
{
    private readonly ILogger<BackgroundWorker> _logger;

    public BackgroundWorker(ILogger<BackgroundWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Worker is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background Worker is running at: {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Background Worker is stopping");
    }
}
