using LiveboxExporter.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Prometheus;

namespace LiveboxExporter
{
    public class Program
    {
        enum Mode
        {
            Default,
            Periodic
        }

        public static async Task Main(string[] args)
        {
            Metrics.SuppressDefaultMetrics();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddSimpleConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]");

            var mode = builder.Configuration.GetValue("Mode", Mode.Default);

            ConfigureForwardedHeadersMiddleware(builder);

            builder.Services.AddSingleton<LiveboxMetricsExporter>();
            if (mode == Mode.Periodic)
            {
                builder.Services.AddHostedService<LiveboxMetricsBackgroundWorker>();
            }

            builder.Services.Configure<LiveboxMetricsBackgroundWorkerOptions>(builder.Configuration);
            builder.Services.Configure<LiveboxMetricsExporterOptions>(builder.Configuration.GetSection("Livebox"));
            builder.Services.AddSingleton<IPostConfigureOptions<LiveboxMetricsExporterOptions>, PostConfigureLiveboxMetricsExporterOptions>();

            ConfigureLiveboxHttpClient(builder);

            var app = builder.Build();
            UseForwardedHeadersMiddleware(app);

            app.Map("/metrics", builder => builder.UseMetricServer(settings => settings.EnableOpenMetrics = false, url: null));
            MapHomepageRoute(app);

            if (mode == Mode.Default)
            {
                var exporter = app.Services.GetRequiredService<LiveboxMetricsExporter>();
                Metrics.DefaultRegistry.AddBeforeCollectCallback(exporter.Scrape);

                // Warm-up (fake scrape to initialize auth context before actual scrapes).
                await exporter.Scrape(app.Lifetime.ApplicationStopping);
            }
            
            app.Run();
        }

        private static void MapHomepageRoute(WebApplication app)
        {
            app.MapGet("/", WriteHomepageResponse);

            // Curiously, when Path=/ and a PathBase is used, previous MapGet("/") is not triggered.
            // This is a workaround for that case.
            app.MapWhen(
                context => context.Request.Path == "/" || context.Request.Path == PathString.Empty,
                builder => builder.Run(WriteHomepageResponse));
        }

        private static Task WriteHomepageResponse(HttpContext context)
        {
            HttpRequest request = context.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}/";
            return context.Response.WriteAsync($"Livebox Exporter is up. See /metrics endpoint ({baseUrl}metrics).");
        }

        private static void ConfigureLiveboxHttpClient(WebApplicationBuilder builder)
        {
            builder.Services.AddTransient<LiveboxAuthorizationHandler>();
            builder.Services.Configure<LiveboxAuthorizationHandlerOptions>(builder.Configuration.GetSection("Livebox"));
            builder.Services.AddHttpClient<LiveboxClient>((services, client) =>
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
        }

        private static void ConfigureForwardedHeadersMiddleware(WebApplicationBuilder builder)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedPrefix;

                // We clear default values so we can add exact values in config if wanted.
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();

                builder.Configuration.Bind(options);
                var knownProxies = builder.Configuration.GetSection("KnownProxies").Get<string[]>();
                if (knownProxies != null)
                {
                    foreach (string item in knownProxies)
                        options.KnownProxies.Add(System.Net.IPAddress.Parse(item));
                }

                var knownetworks = builder.Configuration.GetSection("KnownNetworks").Get<string[]>();
                if (knownetworks != null)
                {
                    foreach (string item in knownetworks)
                        options.KnownNetworks.Add(IPNetwork.Parse(item));
                }

            });
        }

        private static void UseForwardedHeadersMiddleware(WebApplication app)
        {
            app.UseForwardedHeaders();

            // See https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0#work-with-path-base-and-proxies-that-change-the-request-path
            app.UseWhen(context => context.Request.PathBase.HasValue,
                builder => builder.Use(async (context, next) =>
                {
                    if (context.Request.Path.StartsWithSegments(context.Request.PathBase, out var path))
                    {
                        // Basically what UsePathBaseMiddleware would do but its config is static, here it's dynamic.
                        // ForwardedHeadersMiddleware probably should include this logic because
                        // it mutates PathBase but does not mutate Path, so we need to do this
                        // for minimal API route mapping to work.
                        // https://github.com/dotnet/aspnetcore/blob/c094384b2d119e147a205851e7f457149c3bbfb8/src/Http/Http.Abstractions/src/Extensions/UsePathBaseMiddleware.cs#L49-L61
                        context.Request.Path = path;
                    }
                    await next();
                }));
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
    }
}
