using Microsoft.Extensions.Options;

namespace LiveboxExporter.Components
{
    /// <summary>
    /// Optional background service to scrape metrics 
    /// periodically.
    /// </summary>
    public sealed class LiveboxMetricsBackgroundWorker : BackgroundService
    {
        private readonly TimeSpan _timerInterval;
        private readonly LB5MetricsExporter _exporter;
        private readonly ILogger _logger;
        
        public LiveboxMetricsBackgroundWorker(IOptions<LiveboxMetricsBackgroundWorkerOptions> options,
                                              LB5MetricsExporter exporter,
                                              ILogger<LiveboxMetricsBackgroundWorker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _timerInterval = options.Value.TimerInterval ?? LiveboxMetricsBackgroundWorkerOptions.DefaultTimerInterval;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_timerInterval);

            _logger.LogInformation("Timer started");
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _exporter.Scrape(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to collect Livebox metrics.");
                }
            }
        }
    }
}
