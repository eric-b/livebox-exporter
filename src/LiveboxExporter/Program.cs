using LiveboxExporter.Components;
using Prometheus;

namespace LiveboxExporter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Metrics.SuppressDefaultMetrics();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Services.AddHostedService<Components.LiveboxMetricsBackgroundWorker>();

            builder.Services.AddTransient<LiveboxAuthorizationHandler>();
            builder.Services.Configure<LiveboxAuthorizationHandlerOptions>(builder.Configuration.GetSection("Livebox"));
            builder.Services.Configure<LiveboxMetricsBackgroundWorkerOptions>(builder.Configuration.GetSection("Livebox"));

            builder.Services
                .AddHttpClient<LiveboxClient>((services, client) =>
                {
                    string? host = builder.Configuration.GetValue<string>("Livebox:Host");
                    if (!string.IsNullOrEmpty(host))
                    {
                        client.BaseAddress = new Uri($"http://{host}/");
                    }
                    else
                    {
                        client.BaseAddress = TryDiscoverLiveboxAddressOrFail(services.GetRequiredService<ILogger<LiveboxClientDiscovery>>());
                    }
                })
                .AddHttpMessageHandler<LiveboxAuthorizationHandler>();

            var app = builder.Build();

            app.Map("/metrics", builder =>
            {
                builder.UseMetricServer(settings => settings.EnableOpenMetrics = false, url: null);
            });

            app.Map("/", () => "Livebox Exporter is up. See /metrics endpoint.");

            app.Run();
        }

        private static Uri TryDiscoverLiveboxAddressOrFail(ILogger<LiveboxClientDiscovery> logger)
        {
            return Nito.AsyncEx.AsyncContext.Run(() => TryDiscoverLiveboxAddressOrFailAsync(logger));
        }

        private static async Task<Uri> TryDiscoverLiveboxAddressOrFailAsync(ILogger<LiveboxClientDiscovery> logger)
        {
            logger.LogInformation("Livebox address discovery...");
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(3);
                var discovery = new LiveboxClientDiscovery(client, logger);
                LiveboxDiscoveryResult result = await discovery.TryDiscoverLiveboxAddress(default);
                return result.address ?? throw new NotSupportedException("Could not discovery Livebox address. Please configure it in application settings.");
            }
        }

        sealed class EmptyDiscoveryLogger : ILogger<LiveboxClientDiscovery>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
