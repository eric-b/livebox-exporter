using LiveboxExporter.Components.Model;
using LiveboxExporter.Extensions.Dictionary;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace LiveboxExporter.Components
{
    /// <summary>
    /// Core component: collects metrics.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="liveboxClient"></param>
    /// <param name="logger"></param>
    public sealed class LB5MetricsExporter(IOptions<LiveboxMetricsExporterOptions> options,
                                              LiveboxClient liveboxClient,
                                              ILogger<LB5MetricsExporter> logger)
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
            private readonly bool _authIsDisabled;

            private readonly ILogger _logger;

            private NeMoNetDevStats.NeMoNetDevStatsStatus? _lastNeMoNetDevStats;

            private readonly LB5Metrics _metrics;

            public LiveboxMetrics(bool authIsDisabled, ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _authIsDisabled = authIsDisabled;

                _metrics = new LB5Metrics();
            }

            public void Update(SysBusDeviceInfo? deviceInfo,
                               SysBusDevice? device,
                               SysBusNmc? nmc,
                               NeMoNetDevStats? netDevStats)
            {
                var integerValues = new Dictionary<string, long>();
                try
                {
                    bool anyConnectivity = nmc != null || device != null || deviceInfo != null;
                    bool allGood =
                        nmc != null && nmc.Status && nmc.Data != null &&
                        (device != null || _authIsDisabled) &&
                        deviceInfo != null;

                    integerValues.Add(LB5Metrics.exporter_up, MapToBooleanInteger(anyConnectivity));
                    
                    if (deviceInfo != null)
                    {
                        SysBusDeviceInfo.SysBusDeviceInfoStatus status = deviceInfo.Status;
                        if (status.UpTime.HasValue)
                            integerValues.Add(LB5Metrics.device_info_uptime, status.UpTime.Value);
                        if (status.NumberOfReboots.HasValue)
                            integerValues.Add(LB5Metrics.device_info_reboots_total, status.NumberOfReboots.Value);

                        if (status.DeviceStatus != null)
                        {
                            // null if not auth.
                            integerValues.Add(LB5Metrics.device_info_status, MapToBooleanInteger(status.DeviceStatus));
                        }
                    }
                    else if (!_authIsDisabled)
                    {
                        // With or without any connectivity: missing data when auth is enabled = not good device status.
                        integerValues.SetZero(LB5Metrics.device_info_status);
                    }

                    if (device != null && device.Status != null)
                    {
                        SysBusDevice.SysBusDeviceStatus status = device.Status;
                        integerValues.Add(LB5Metrics.device_downstream_curr_rate, status.DownstreamCurrRate);
                        integerValues.Add(LB5Metrics.device_upstream_curr_rate, status.UpstreamCurrRate);
                        integerValues.Add(LB5Metrics.device_downstream_max_bit_rate, status.DownstreamMaxBitRate);
                        integerValues.Add(LB5Metrics.device_upstream_max_bit_rate, status.UpstreamMaxBitRate);

                        integerValues.Add(LB5Metrics.device_active, MapToBooleanInteger(status.Active));
                        integerValues.Add(LB5Metrics.device_link_state, MapToBooleanInteger(status.LinkState));
                        integerValues.Add(LB5Metrics.device_connection_state, MapToBooleanInteger(status.ConnectionState));
                        integerValues.Add(LB5Metrics.device_internet, MapToBooleanInteger(status.Internet));
                        integerValues.Add(LB5Metrics.device_telephony, MapToBooleanInteger(status.Telephony));
                        integerValues.Add(LB5Metrics.device_iptv, MapToBooleanInteger(status.IPTV));
                    }
                    else if (anyConnectivity && !_authIsDisabled)
                    {
                        // None of this data is available if not auth.
                        integerValues.SetZero(LB5Metrics.device_downstream_curr_rate,
                                              LB5Metrics.device_upstream_curr_rate,
                                              LB5Metrics.device_active,
                                              LB5Metrics.device_link_state,
                                              LB5Metrics.device_connection_state,
                                              LB5Metrics.device_internet,
                                              LB5Metrics.device_telephony,
                                              LB5Metrics.device_iptv);
                    }

                    if (netDevStats != null && netDevStats.Status != null)
                    {
                        NeMoNetDevStats.NeMoNetDevStatsStatus status = netDevStats.Status;
                        integerValues.Add(LB5Metrics.ont_rx_packets, status.RxPackets);
                        integerValues.Add(LB5Metrics.ont_tx_packets, status.TxPackets);
                        integerValues.Add(LB5Metrics.ont_rx_bytes, status.RxBytes);
                        integerValues.Add(LB5Metrics.ont_tx_bytes, status.TxBytes);
                        integerValues.Add(LB5Metrics.ont_rx_errors, status.RxErrors);
                        integerValues.Add(LB5Metrics.ont_tx_errors, status.TxErrors);
                        integerValues.Add(LB5Metrics.ont_rx_dropped, status.RxDropped);
                        integerValues.Add(LB5Metrics.ont_tx_dropped, status.TxDropped);
                        integerValues.Add(LB5Metrics.ont_multicast, status.Multicast);
                        integerValues.Add(LB5Metrics.ont_collisions, status.Collisions);
                        integerValues.Add(LB5Metrics.ont_rx_length_errors, status.RxLengthErrors);
                        integerValues.Add(LB5Metrics.ont_rx_over_errors, status.RxOverErrors);
                        integerValues.Add(LB5Metrics.ont_rx_crc_errors, status.RxCrcErrors);
                        integerValues.Add(LB5Metrics.ont_rx_frame_errors, status.RxFrameErrors);
                        integerValues.Add(LB5Metrics.ont_rx_fifo_errors, status.RxFifoErrors);
                        integerValues.Add(LB5Metrics.ont_rx_missed_errors, status.RxMissedErrors);
                        integerValues.Add(LB5Metrics.ont_tx_aborted_errors, status.TxAbortedErrors);
                        integerValues.Add(LB5Metrics.ont_tx_carrier_errors, status.TxCarrierErrors);
                        integerValues.Add(LB5Metrics.ont_tx_fifo_errors, status.TxFifoErrors);
                        integerValues.Add(LB5Metrics.ont_tx_heartbeat_errors, status.TxHeartbeatErrors);
                        integerValues.Add(LB5Metrics.ont_tx_window_errors, status.TxWindowErrors);

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
                                    integerValues.Add(LB5Metrics.ont_rx_bit_rate, (long)Math.Round((rx2 - rx1) * 8 / elaspedSecondsSinceLastMeasure));
                                    integerValues.Add(LB5Metrics.ont_tx_bit_rate, (long)Math.Round((tx2 - tx1) * 8 / elaspedSecondsSinceLastMeasure));
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
                        integerValues.SetZero(LB5Metrics.ont_rx_packets,
                                              LB5Metrics.ont_tx_packets,
                                              LB5Metrics.ont_rx_bytes,
                                              LB5Metrics.ont_tx_bytes,
                                              LB5Metrics.ont_rx_errors,
                                              LB5Metrics.ont_tx_errors,
                                              LB5Metrics.ont_rx_dropped,
                                              LB5Metrics.ont_tx_dropped,
                                              LB5Metrics.ont_multicast,
                                              LB5Metrics.ont_collisions,
                                              LB5Metrics.ont_rx_length_errors,
                                              LB5Metrics.ont_rx_over_errors,
                                              LB5Metrics.ont_rx_crc_errors,
                                              LB5Metrics.ont_rx_frame_errors,
                                              LB5Metrics.ont_rx_fifo_errors,
                                              LB5Metrics.ont_rx_missed_errors,
                                              LB5Metrics.ont_tx_aborted_errors,
                                              LB5Metrics.ont_tx_carrier_errors,
                                              LB5Metrics.ont_tx_fifo_errors,
                                              LB5Metrics.ont_tx_heartbeat_errors,
                                              LB5Metrics.ont_tx_window_errors,
                                              LB5Metrics.ont_rx_bit_rate,
                                              LB5Metrics.ont_tx_bit_rate);
                    }

                    if (nmc != null && nmc.Status && nmc.Data != null)
                    {
                        // All this is available even when not auth.
                        SysBusNmc.SysBusNmcData data = nmc.Data;
                        integerValues.Add(LB5Metrics.nmc_gpon_state, data.GponState == "O5_Operation" ? 1 : 0);
                        integerValues.Add(LB5Metrics.nmc_connection_state, MapToBooleanInteger(data.ConnectionState));
                        integerValues.Add(LB5Metrics.nmc_link_state, MapToBooleanInteger(data.LinkState));
                        integerValues.Add(LB5Metrics.nmc_wan_state, MapToBooleanInteger(data.WanState));
                    }
                    else if (anyConnectivity)
                    {
                        integerValues.SetZero(LB5Metrics.nmc_gpon_state,
                                              LB5Metrics.nmc_connection_state,
                                              LB5Metrics.nmc_link_state,
                                              LB5Metrics.nmc_wan_state);
                    }

                    integerValues.Add(LB5Metrics.exporter_metrics_up, MapToBooleanInteger(allGood));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception occured while updating metric values: {ex.Message}");
                    integerValues.SetZero(LB5Metrics.exporter_metrics_up,
                                          LB5Metrics.exporter_up);
                }

                _metrics.Update(integerValues);
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
