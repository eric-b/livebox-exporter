
using LiveboxExporter.Components.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Prometheus;

namespace LiveboxExporter.Components
{
    public sealed class LiveboxMetricsBackgroundWorker : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly bool _authIsDisabled;
        private readonly LiveboxClient _liveboxClient;
        private readonly LiveboxMetrics _metrics;
        private SysBusDeviceInfo? _lastDeviceInfo;
        private readonly TimeSpan _timerInterval;

        private sealed class LiveboxMetrics
        {
            /*
            Recommended order metrics to display:
            Exporter Up (scraping ok)
            Connection state 
            GPON state 
            *
            device info Status (last to pass)
             */
            private const string MetricPrefix = "livebox_";

            private readonly bool _authIsDisabled;

            private readonly ILogger _logger;

            private readonly Counter 
                _deviceInfoNumberOfReboots;

            private readonly Gauge _exporterIsUp, _exporterMetricsOk;

            private readonly Gauge 
                _deviceInfoUpTime,
                _deviceInfoStatus;

            private readonly Gauge
                _deviceActive,
                _deviceLinkState,
                _deviceConnectionState,
                _deviceInternet,
                _deviceIpTv,
                _deviceTelephony,
                _deviceDownstreamCurrRate,
                _deviceUpstreamCurrRate,
                _deviceDownstreamMaxBitRate,
                _deviceUpstreamMaxBitRate;

            private readonly Gauge
                _nmcWanState,
                _nmcLinkState,
                _nmcGponState,
                _nmcConnectionState;

            public LiveboxMetrics(bool authIsDisabled, ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _authIsDisabled = authIsDisabled;

                var gaugeConfig = new GaugeConfiguration { SuppressInitialValue = true };
                var counterConfig = new CounterConfiguration { SuppressInitialValue = true };

                _exporterIsUp = Metrics.CreateGauge(MetricPrefix + "exporter_up", "Connectivity with Livebox is up (last cached value)", gaugeConfig);
                _exporterMetricsOk = Metrics.CreateGauge(MetricPrefix + "exporter_metrics_up", "All exporter metrics are up to date (last cached value)", gaugeConfig);

                _deviceInfoUpTime = Metrics.CreateGauge(MetricPrefix + "device_info_uptime", "Up Time (last cached value)", gaugeConfig);
                _deviceInfoStatus = Metrics.CreateGauge(MetricPrefix + "device_info_status", "Device Status (last cached value)", gaugeConfig);
                _deviceInfoNumberOfReboots = Metrics.CreateCounter(MetricPrefix + "device_info_reboots_total", "Number of Reboots (last cached value)", counterConfig);

                _deviceActive = Metrics.CreateGauge(MetricPrefix + "device_active", "Device Active (last cached value)", gaugeConfig);
                _deviceLinkState = Metrics.CreateGauge(MetricPrefix + "device_link_state", "Device Link State (last cached value)", gaugeConfig);
                _deviceConnectionState = Metrics.CreateGauge(MetricPrefix + "device_connection_state", "Device Connection State (last cached value)", gaugeConfig);

                _deviceInternet = Metrics.CreateGauge(MetricPrefix + "device_internet", "Internet enabled (last cached value)", gaugeConfig);
                _deviceIpTv = Metrics.CreateGauge(MetricPrefix + "device_iptv", "IPTV enabled (last cached value)", gaugeConfig);
                _deviceTelephony = Metrics.CreateGauge(MetricPrefix + "device_telephony", "Telephony enabled (last cached value)", gaugeConfig);
                _deviceDownstreamCurrRate = Metrics.CreateGauge(MetricPrefix + "device_downstream_curr_rate", "Downstream current rate (last cached value)", gaugeConfig);
                _deviceUpstreamCurrRate = Metrics.CreateGauge(MetricPrefix + "device_upstream_curr_rate", "Upstream current rate (last cached value)", gaugeConfig);
                _deviceDownstreamMaxBitRate = Metrics.CreateGauge(MetricPrefix + "device_downstream_max_bit_rate", "Downstream max bit rate (last cached value)", gaugeConfig);
                _deviceUpstreamMaxBitRate = Metrics.CreateGauge(MetricPrefix + "device_upstream_max_bit_rate", "Upstream max bit rate (last cached value)", gaugeConfig);

                _nmcWanState = Metrics.CreateGauge(MetricPrefix + "nmc_wan_state", "NMC WAN state (last cached value)", gaugeConfig);
                _nmcLinkState = Metrics.CreateGauge(MetricPrefix + "nmc_link_state", "NMC Link state (last cached value)", gaugeConfig);
                _nmcGponState = Metrics.CreateGauge(MetricPrefix + "nmc_gpon_state", "NMC GPON state (last cached value)", gaugeConfig);
                _nmcConnectionState = Metrics.CreateGauge(MetricPrefix + "nmc_connection_state", "NMC Connection state (last cached value)", gaugeConfig);
            }

            public void Update(SysBusDeviceInfo? deviceInfo, SysBusDevice? device, SysBusNmc? nmc)
            {
                try
                {
                    bool anyConnectivity = nmc != null || device != null || deviceInfo != null;
                    bool allGood =
                        nmc != null && nmc.status && nmc.data != null &&
                        (device != null || _authIsDisabled) &&
                        deviceInfo != null;

                    _exporterIsUp.Set(MapToBooleanInteger(anyConnectivity));

                    if (deviceInfo != null)
                    {
                        SysBusDeviceInfo.Status status = deviceInfo.status;
                        if (status.UpTime.HasValue)
                            _deviceInfoUpTime.Set(status.UpTime.Value);
                        if (status.NumberOfReboots.HasValue)
                            _deviceInfoNumberOfReboots.IncTo(status.NumberOfReboots.Value);

                        if (status.DeviceStatus != null)
                        {
                            // null if not auth.
                            _deviceInfoStatus.Set(MapToBooleanInteger(status.DeviceStatus));
                        }
                    }
                    else if (!_authIsDisabled)
                    {
                        // With or without any connectivity: missing data when auth is enabled = not good device status.
                        _deviceInfoStatus.Set(0);
                    }

                    if (device != null && device.status != null)
                    {
                        SysBusDevice.Status status = device.status;
                        _deviceDownstreamCurrRate.Set(status.DownstreamCurrRate);
                        _deviceUpstreamCurrRate.Set(status.UpstreamCurrRate);
                        _deviceDownstreamMaxBitRate.Set(status.DownstreamMaxBitRate);
                        _deviceUpstreamMaxBitRate.Set(status.UpstreamMaxBitRate);

                        _deviceActive.Set(MapToBooleanInteger(status.Active));
                        _deviceLinkState.Set(MapToBooleanInteger(status.LinkState));
                        _deviceConnectionState.Set(MapToBooleanInteger(status.ConnectionState));
                        _deviceInternet.Set(MapToBooleanInteger(status.Internet));
                        _deviceTelephony.Set(MapToBooleanInteger(status.Telephony));
                        _deviceIpTv.Set(MapToBooleanInteger(status.IPTV));
                    }
                    else if (anyConnectivity && !_authIsDisabled)
                    {
                        // None of this data is available if not auth.
                        _deviceDownstreamCurrRate.Set(0);
                        _deviceUpstreamCurrRate.Set(0);
                        _deviceActive.Set(0);
                        _deviceLinkState.Set(0);
                        _deviceConnectionState.Set(0);
                        _deviceInternet.Set(0);
                        _deviceTelephony.Set(0);
                        _deviceIpTv.Set(0);
                    }

                    if (nmc != null && nmc.status && nmc.data != null)
                    {
                        // All this is available even when not auth.
                        SysBusNmc.Data data = nmc.data;
                        _nmcGponState.Set(data.GponState == "O5_Operation" ? 1 : 0);
                        _nmcConnectionState.Set(MapToBooleanInteger(data.ConnectionState));
                        _nmcLinkState.Set(MapToBooleanInteger(data.LinkState));
                        _nmcWanState.Set(MapToBooleanInteger(data.WanState));
                    }
                    else if (anyConnectivity)
                    {
                        _nmcGponState.Set(0);
                        _nmcConnectionState.Set(0);
                        _nmcLinkState.Set(0);
                        _nmcWanState.Set(0);
                    }

                    _exporterMetricsOk.Set(MapToBooleanInteger(allGood));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception occured while updating metric values: {ex.Message}");
                    _exporterIsUp.Set(0);
                    _exporterMetricsOk.Set(0);
                }
            }

            private static int MapToBooleanInteger(bool value)
            {
                return value ? 1 : 0;
            }

            private static int MapToBooleanInteger(string? upOrBoundValue)
            {
                switch (upOrBoundValue)
                {
                    case "Up":
                    case "up":
                    case "Bound":
                        return 1;
                    default:
                        return 0;
                }
            }
        }

        public LiveboxMetricsBackgroundWorker(IOptions<LiveboxMetricsBackgroundWorkerOptions> options,
                                              LiveboxClient liveboxClient,
                                              ILogger<LiveboxMetricsBackgroundWorker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _liveboxClient = liveboxClient ?? throw new ArgumentNullException(nameof(liveboxClient));
            _authIsDisabled = options.Value.AuthIsDisabled;
            _timerInterval = options.Value.TimerInterval ?? LiveboxMetricsBackgroundWorkerOptions.DefaultTimerInterval;
            _metrics = new LiveboxMetrics(_authIsDisabled, logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_timerInterval);

            _logger.LogInformation("Timer started");
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await TimerElapsed(stoppingToken);
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

        enum ScrapeStatus
        {
            Success,
            ReAuthRequired,
            Error
        }

        private async Task<ScrapeStatus> Scrape(bool forceReAuth, CancellationToken cancellationToken)
        {
            SysBusNmc? nmc = null;
            SysBusDeviceInfo? deviceInfo = null;
            SysBusDevice? device = null;
            try
            {
                if (forceReAuth ||
                    _lastDeviceInfo is null ||
                    _lastDeviceInfo.status.DeviceStatus != "Up" ||
                    Random.Shared.NextDouble() > 0.5)
                {
                    // We avoid getting device info each time.
                    if (forceReAuth)
                    {
                        _liveboxClient.ForceAuthOnNextRequest();
                    }

                    deviceInfo = await TryGetDeviceInfo(cancellationToken);
                    if (deviceInfo != null && deviceInfo.status is null)
                    {
                        // unexpected.
                        deviceInfo = null; 
                    }

                    if (deviceInfo != null &&
                        deviceInfo.status != null &&
                        deviceInfo.status.DeviceStatus != null)
                    {
                        // Gathered info from device info is incomplete when auth is invalid/expired.
                        // Cache only a full valid state.
                        _lastDeviceInfo = deviceInfo;
                    }
                }
                else
                {
                    deviceInfo = _lastDeviceInfo;
                }

                if (deviceInfo is not null)
                {
                    // gathered info from device is available ONLY when auth context is valid.
                    device = await TryGetDevice(deviceInfo, cancellationToken);
                    if (device != null)
                    {
                        if (device.errors != null && device.errors.Length != 0)
                        {
                            if (!forceReAuth && 
                                device.errors.Any(t => t.error == 13))
                            {
                                // Permission denied
                                if (!_authIsDisabled)
                                {
                                    return ScrapeStatus.ReAuthRequired;
                                }
                            }
                            else
                            {
                                _logger.LogError(string.Join(Environment.NewLine, device.errors.Select(t => $"{t.error}: {t.description}")));
                            }
                        }

                        if (device.status is null)
                        {
                            device = null;
                        }
                    }
                }

                // gathered info from nmc is available even when auth is invalid/expired.
                nmc = await TryGetNmcWanStatus(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception occured while scraping metrics: {ex.Message}");
                _metrics.Update(deviceInfo, device, nmc);
                return ScrapeStatus.Error;
            }
            _metrics.Update(deviceInfo, device, nmc);
            return ScrapeStatus.Success;
        }

        private async Task TimerElapsed(CancellationToken cancellationToken)
        {
            ScrapeStatus status = await Scrape(false, cancellationToken);
            if (status == ScrapeStatus.ReAuthRequired)
            {
                _logger.LogInformation("Auth context seems invalid or expired. Retry with new auth...");
                status = await Scrape(true, cancellationToken);
                if (status == ScrapeStatus.ReAuthRequired)
                {
                    _logger.LogError("Unable to scrape metrics due to authentication issue (maybe due to unexpected responses from livebox).");
                }
            }
        }

        private async Task<SysBusDeviceInfo?> TryGetDeviceInfo(CancellationToken cancellationToken)
        {
            string? raw = await _liveboxClient.RawCallFunctionWithoutParameter("sysbus.DeviceInfo", "get", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusDeviceInfo>(raw);
        }

        private async Task<SysBusDevice?> TryGetDevice(SysBusDeviceInfo info, CancellationToken cancellationToken)
        {
            string? raw = await _liveboxClient.RawCallFunctionWithoutParameter("sysbus.Devices.Device." + info.status.BaseMAC.ToUpper(), "get", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusDevice>(raw);
        }

        private async Task<SysBusNmc?> TryGetNmcWanStatus(CancellationToken cancellationToken)
        {
            string? raw = await _liveboxClient.RawCallFunctionWithoutParameter("sysbus.NMC", "getWANStatus", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusNmc>(raw);
        }
    }
}
