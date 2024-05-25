using LiveboxExporter.Components.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Prometheus;

namespace LiveboxExporter.Components
{
    /// <summary>
    /// Core component: collects metrics.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="liveboxClient"></param>
    /// <param name="logger"></param>
    public sealed class LiveboxMetricsExporter(IOptions<LiveboxMetricsExporterOptions> options,
                                              LiveboxClient liveboxClient,
                                              ILogger<LiveboxMetricsExporter> logger)
    {
        private readonly bool _authIsDisabled = options.Value.AuthIsDisabled;
        private readonly LiveboxMetrics _metrics = new LiveboxMetrics(options.Value.AuthIsDisabled, logger);
        private SysBusDeviceInfo? _lastDeviceInfo;

        enum ScrapeStatus
        {
            Success,
            ReAuthRequired,
            Error
        }

        private sealed class LiveboxMetrics
        {
            private const string MetricPrefix = "livebox_";

            private readonly bool _authIsDisabled;

            private readonly ILogger _logger;

            private NeMoNetDevStats.NeMoNetDevStatsStatus? _lastNeMoNetDevStats;
            
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
                _ontRxPackets,
                _ontTxPackets,
                _ontRxBytes,
                _ontTxBytes,
                _ontRxErrors,
                _ontTxErrors,
                _ontRxDropped,
                _ontTxDropped,
                _ontMulticast,
                _ontCollisions,
                _ontRxLengthErrors,
                _ontRxOverErrors,
                _ontRxCrcErrors,
                _ontRxFrameErrors,
                _ontRxFifoErrors,
                _ontRxMissedErrors,
                _ontTxAbortedErrors,
                _ontTxCarrierErrors,
                _ontTxFifoErrors,
                _ontTxHeartbeatErrors,
                _ontTxWindowErrors,
                _ontRxBitRate,
                _ontTxBitRate;

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

                _exporterIsUp = Metrics.CreateGauge(MetricPrefix + "exporter_up", "Connectivity with Livebox is up", gaugeConfig);
                _exporterMetricsOk = Metrics.CreateGauge(MetricPrefix + "exporter_metrics_up", "All exporter metrics are up to date", gaugeConfig);

                _deviceInfoUpTime = Metrics.CreateGauge(MetricPrefix + "device_info_uptime", "Up Time", gaugeConfig);
                _deviceInfoStatus = Metrics.CreateGauge(MetricPrefix + "device_info_status", "Device Status", gaugeConfig);
                _deviceInfoNumberOfReboots = Metrics.CreateCounter(MetricPrefix + "device_info_reboots_total", "Number of Reboots", counterConfig);

                _deviceActive = Metrics.CreateGauge(MetricPrefix + "device_active", "Device Active", gaugeConfig);
                _deviceLinkState = Metrics.CreateGauge(MetricPrefix + "device_link_state", "Device Link State", gaugeConfig);
                _deviceConnectionState = Metrics.CreateGauge(MetricPrefix + "device_connection_state", "Device Connection State", gaugeConfig);

                _deviceInternet = Metrics.CreateGauge(MetricPrefix + "device_internet", "Internet enabled", gaugeConfig);
                _deviceIpTv = Metrics.CreateGauge(MetricPrefix + "device_iptv", "IPTV enabled", gaugeConfig);
                _deviceTelephony = Metrics.CreateGauge(MetricPrefix + "device_telephony", "Telephony enabled", gaugeConfig);
                _deviceDownstreamCurrRate = Metrics.CreateGauge(MetricPrefix + "device_downstream_curr_rate", "Downstream current rate", gaugeConfig);
                _deviceUpstreamCurrRate = Metrics.CreateGauge(MetricPrefix + "device_upstream_curr_rate", "Upstream current rate", gaugeConfig);
                _deviceDownstreamMaxBitRate = Metrics.CreateGauge(MetricPrefix + "device_downstream_max_bit_rate", "Downstream max bit rate", gaugeConfig);
                _deviceUpstreamMaxBitRate = Metrics.CreateGauge(MetricPrefix + "device_upstream_max_bit_rate", "Upstream max bit rate", gaugeConfig);

                _ontRxPackets = Metrics.CreateGauge(MetricPrefix + "ont_rx_packets", "VEIP0 stats (RX packets)", gaugeConfig);
                _ontTxPackets = Metrics.CreateGauge(MetricPrefix + "ont_tx_packets", "VEIP0 stats (TX packets)", gaugeConfig);
                _ontRxBytes = Metrics.CreateGauge(MetricPrefix + "ont_rx_bytes", "VEIP0 stats (RX bytes)", gaugeConfig);
                _ontTxBytes = Metrics.CreateGauge(MetricPrefix + "ont_tx_bytes", "VEIP0 stats (TX bytes)", gaugeConfig);
                _ontRxErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_errors", "VEIP0 stats (RX errors)", gaugeConfig);
                _ontTxErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_errors", "VEIP0 stats (TX errors)", gaugeConfig);
                _ontRxDropped = Metrics.CreateGauge(MetricPrefix + "ont_rx_dropped", "VEIP0 stats (RX dropped)", gaugeConfig);
                _ontTxDropped = Metrics.CreateGauge(MetricPrefix + "ont_tx_dropped", "VEIP0 stats (TX dropped)", gaugeConfig);
                _ontMulticast = Metrics.CreateGauge(MetricPrefix + "ont_multicast", "VEIP0 stats (multicast)", gaugeConfig);
                _ontCollisions = Metrics.CreateGauge(MetricPrefix + "ont_collisions", "VEIP0 stats (collisions)", gaugeConfig);
                _ontRxLengthErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_length_errors", "VEIP0 stats (RX length errors)", gaugeConfig);
                _ontRxOverErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_over_errors", "VEIP0 stats (RX Over errors)", gaugeConfig);
                _ontRxCrcErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_crc_errors", "VEIP0 stats (RX CRC errors)", gaugeConfig);
                _ontRxFrameErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_frame_errors", "VEIP0 stats (RX frame errors)", gaugeConfig);
                _ontRxFifoErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_fifo_errors", "VEIP0 stats (RX FIFO errors)", gaugeConfig);
                _ontRxMissedErrors = Metrics.CreateGauge(MetricPrefix + "ont_rx_missed_errors", "VEIP0 stats (RX missed errors)", gaugeConfig);
                _ontTxAbortedErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_aborted_errors", "VEIP0 stats (TX aborted errors)", gaugeConfig);
                _ontTxCarrierErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_carrier_errors", "VEIP0 stats (TX carrier errors)", gaugeConfig);
                _ontTxFifoErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_fifo_errors", "VEIP0 stats (TX FIFO errors)", gaugeConfig);
                _ontTxHeartbeatErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_heartbeat_errors", "VEIP0 stats (TX heartbeat errors)", gaugeConfig);
                _ontTxWindowErrors = Metrics.CreateGauge(MetricPrefix + "ont_tx_window_errors", "VEIP0 stats (TX window errors)", gaugeConfig);

                _ontRxBitRate = Metrics.CreateGauge(MetricPrefix + "ont_rx_bit_rate", "VEIP0 stats (RX computed bit rate)", gaugeConfig);
                _ontTxBitRate = Metrics.CreateGauge(MetricPrefix + "ont_tx_bit_rate", "VEIP0 stats (TX computed bit rate)", gaugeConfig);

                _nmcWanState = Metrics.CreateGauge(MetricPrefix + "nmc_wan_state", "NMC WAN state", gaugeConfig);
                _nmcLinkState = Metrics.CreateGauge(MetricPrefix + "nmc_link_state", "NMC Link state", gaugeConfig);
                _nmcGponState = Metrics.CreateGauge(MetricPrefix + "nmc_gpon_state", "NMC GPON state", gaugeConfig);
                _nmcConnectionState = Metrics.CreateGauge(MetricPrefix + "nmc_connection_state", "NMC Connection state", gaugeConfig);
            }

            public void Update(SysBusDeviceInfo? deviceInfo,
                               SysBusDevice? device,
                               SysBusNmc? nmc,
                               NeMoNetDevStats? netDevStats)
            {
                try
                {
                    bool anyConnectivity = nmc != null || device != null || deviceInfo != null;
                    bool allGood =
                        nmc != null && nmc.Status && nmc.Data != null &&
                        (device != null || _authIsDisabled) &&
                        deviceInfo != null;

                    _exporterIsUp.Set(MapToBooleanInteger(anyConnectivity));

                    if (deviceInfo != null)
                    {
                        SysBusDeviceInfo.SysBusDeviceInfoStatus status = deviceInfo.Status;
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

                    if (device != null && device.Status != null)
                    {
                        SysBusDevice.SysBusDeviceStatus status = device.Status;
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

                    if (netDevStats != null && netDevStats.Status != null)
                    {
                        NeMoNetDevStats.NeMoNetDevStatsStatus status = netDevStats.Status;
                        _ontRxPackets.Set(status.RxPackets);
                        _ontTxPackets.Set(status.TxPackets);
                        _ontRxBytes.Set(status.RxBytes);
                        _ontTxBytes.Set(status.TxBytes);
                        _ontRxErrors.Set(status.RxErrors);
                        _ontTxErrors.Set(status.TxErrors);
                        _ontRxDropped.Set(status.RxDropped);
                        _ontTxDropped.Set(status.TxDropped);
                        _ontMulticast.Set(status.Multicast);
                        _ontCollisions.Set(status.Collisions);
                        _ontRxLengthErrors.Set(status.RxLengthErrors);
                        _ontRxOverErrors.Set(status.RxOverErrors);
                        _ontRxCrcErrors.Set(status.RxCrcErrors);
                        _ontRxFrameErrors.Set(status.RxFrameErrors);
                        _ontRxFifoErrors.Set(status.RxFifoErrors);
                        _ontRxMissedErrors.Set(status.RxMissedErrors);
                        _ontTxAbortedErrors.Set(status.TxAbortedErrors);
                        _ontTxCarrierErrors.Set(status.TxCarrierErrors);
                        _ontTxFifoErrors.Set(status.TxFifoErrors);
                        _ontTxHeartbeatErrors.Set(status.TxHeartbeatErrors);
                        _ontTxWindowErrors.Set(status.TxWindowErrors);

                        bool keeplastNeMoNetDevStats = false;
                        if (_lastNeMoNetDevStats != null)
                        {
                            long rx1 = _lastNeMoNetDevStats.RxBytes;
                            long tx1 = _lastNeMoNetDevStats.TxBytes;
                            long rx2 = status.RxBytes;
                            long tx2 = status.TxBytes;
                            if (rx2 >= rx1 && tx2 >= tx1)
                            {
                                double elaspedSecondsSinceLastMeasure = status.GetElapsedTimeSince(_lastNeMoNetDevStats).TotalSeconds;
                                if (elaspedSecondsSinceLastMeasure > 1)
                                {
                                    _ontRxBitRate.Set(Math.Round((rx2 - rx1) * 8 / elaspedSecondsSinceLastMeasure));
                                    _ontTxBitRate.Set(Math.Round((tx2 - tx1) * 8 / elaspedSecondsSinceLastMeasure));
                                }
                                else
                                {
                                    keeplastNeMoNetDevStats = true;
                                }
                            }
                        }
                        
                        if (!keeplastNeMoNetDevStats)
                        {
                            _lastNeMoNetDevStats = status;
                        }
                    }
                    else if (anyConnectivity && !_authIsDisabled)
                    {
                        // None of this data is available if not auth.
                        _lastNeMoNetDevStats = null;
                        _ontRxPackets.Set(0);
                        _ontTxPackets.Set(0);
                        _ontRxBytes.Set(0);
                        _ontTxBytes.Set(0);
                        _ontRxErrors.Set(0);
                        _ontTxErrors.Set(0);
                        _ontRxDropped.Set(0);
                        _ontTxDropped.Set(0);
                        _ontMulticast.Set(0);
                        _ontCollisions.Set(0);
                        _ontRxLengthErrors.Set(0);
                        _ontRxOverErrors.Set(0);
                        _ontRxCrcErrors.Set(0);
                        _ontRxFrameErrors.Set(0);
                        _ontRxFifoErrors.Set(0);
                        _ontRxMissedErrors.Set(0);
                        _ontTxAbortedErrors.Set(0);
                        _ontTxCarrierErrors.Set(0);
                        _ontTxFifoErrors.Set(0);
                        _ontTxHeartbeatErrors.Set(0);
                        _ontTxWindowErrors.Set(0);
                        _ontRxBitRate.Set(0);
                        _ontTxBitRate.Set(0);
                    }

                    if (nmc != null && nmc.Status && nmc.Data != null)
                    {
                        // All this is available even when not auth.
                        SysBusNmc.SysBusNmcData data = nmc.Data;
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
                return upOrBoundValue switch
                {
                    "Up" or "up" or "Bound" => 1,
                    _ => 0,
                };
            }
        }

        public async Task Scrape(CancellationToken cancellationToken)
        {
            ScrapeStatus status = await Scrape(false, cancellationToken);
            if (status == ScrapeStatus.ReAuthRequired)
            {
                logger.LogInformation("Auth context seems invalid or expired. Retry with new auth...");
                status = await Scrape(true, cancellationToken);
                if (status == ScrapeStatus.ReAuthRequired)
                {
                    logger.LogError("Unable to scrape metrics due to authentication issue (maybe due to unexpected responses from livebox).");
                }
            }
        }

        private async Task<ScrapeStatus> Scrape(bool forceReAuth, CancellationToken cancellationToken)
        {
            SysBusNmc? nmc = null;
            SysBusDeviceInfo? deviceInfo = null;
            SysBusDevice? device = null;
            NeMoNetDevStats? netDevStat = null;
            try
            {
                bool alreadyReAuth = false;
                if (forceReAuth ||
                    _lastDeviceInfo is null ||
                    _lastDeviceInfo.Status.DeviceStatus != "Up" ||
                    Random.Shared.NextDouble() > 0.5)
                {
                    // We avoid getting device info each time.
                    if (forceReAuth)
                    {
                        liveboxClient.ForceAuthOnNextRequest();
                        alreadyReAuth = true;
                    }

                    deviceInfo = await TryGetDeviceInfo(cancellationToken);
                    if (deviceInfo != null && deviceInfo.Status is null)
                    {
                        // unexpected.
                        deviceInfo = null;
                    }

                    if (deviceInfo != null &&
                        deviceInfo.Status != null &&
                        deviceInfo.Status.DeviceStatus != null)
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

                if (deviceInfo is not null && !_authIsDisabled)
                {
                    // gathered info from device is available ONLY when auth context is valid.
                    device = await TryGetDevice(deviceInfo, cancellationToken);
                    if (device != null)
                    {
                        if (ShouldReAuth(device, alreadyReAuth))
                        {
                            return ScrapeStatus.ReAuthRequired;
                        }

                        if (device.Status is null)
                        {
                            device = null;
                        }
                    }

                    // gathered info from netDevStat is available ONLY when auth context is valid.
                    netDevStat = await TryGetVeip0NetDevStats(cancellationToken);
                    if (netDevStat != null)
                    {
                        if (ShouldReAuth(netDevStat, alreadyReAuth))
                        {
                            return ScrapeStatus.ReAuthRequired;
                        }
                        
                        if (netDevStat.Status is null)
                        {
                            netDevStat = null;
                        }
                    }
                }

                // gathered info from nmc is available even when auth is invalid/expired.
                nmc = await TryGetNmcWanStatus(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Exception occured while scraping metrics: {ex.Message}");
                _metrics.Update(deviceInfo, device, nmc, netDevStat);
                return ScrapeStatus.Error;
            }
            _metrics.Update(deviceInfo, device, nmc, netDevStat);
            return ScrapeStatus.Success;
        }

        private bool ShouldReAuth(IWithError statusWithError, bool disableReAuth)
        {
            if (statusWithError.Errors != null && statusWithError.Errors.Length != 0)
            {
                bool authError = statusWithError.Errors.Any(t => t.Error == 13);
                if (!authError)
                {
                    logger.LogError(string.Join(Environment.NewLine, statusWithError.Errors.Select(t => $"{t.Error}: {t.Description}")));
                }
                return authError && !disableReAuth;
            }
            return false;
        }

        private async Task<SysBusDeviceInfo?> TryGetDeviceInfo(CancellationToken cancellationToken)
        {
            string? raw = await liveboxClient.RawCallFunctionWithoutParameter("sysbus.DeviceInfo", "get", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusDeviceInfo>(raw);
        }

        private async Task<SysBusDevice?> TryGetDevice(SysBusDeviceInfo info, CancellationToken cancellationToken)
        {
            string? raw = await liveboxClient.RawCallFunctionWithoutParameter("sysbus.Devices.Device." + info.Status.BaseMAC.ToUpper(), "get", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusDevice>(raw);
        }

        private async Task<SysBusNmc?> TryGetNmcWanStatus(CancellationToken cancellationToken)
        {
            string? raw = await liveboxClient.RawCallFunctionWithoutParameter("sysbus.NMC", "getWANStatus", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<SysBusNmc>(raw);
        }

        private async Task<NeMoNetDevStats?> TryGetVeip0NetDevStats(CancellationToken cancellationToken)
        {
            string? raw = await liveboxClient.RawCallFunctionWithoutParameter("NeMo.Intf.veip0", "getNetDevStats", cancellationToken);
            return string.IsNullOrEmpty(raw) ? null : JsonConvert.DeserializeObject<NeMoNetDevStats>(raw);
        }
    }
}
