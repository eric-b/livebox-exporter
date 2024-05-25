using Prometheus;

namespace LiveboxExporter.Components
{
    public sealed class LB5Metrics
    {
        private readonly Dictionary<string, Counter> _counters;

        private readonly Dictionary<string, Gauge> _gauges;

        public const string
            exporter_up = "livebox_exporter_up",
            exporter_metrics_up = "livebox_exporter_metrics_up",
            device_info_uptime = "livebox_device_info_uptime",
            device_info_status = "livebox_device_info_status",
            device_info_reboots_total = "livebox_device_info_reboots_total",
            device_active = "livebox_device_active",
            device_link_state = "livebox_device_link_state",
            device_connection_state = "livebox_device_connection_state",
            device_internet = "livebox_device_internet",
            device_iptv = "livebox_device_iptv",
            device_telephony = "livebox_device_telephony",
            device_downstream_curr_rate = "livebox_device_downstream_curr_rate",
            device_upstream_curr_rate = "livebox_device_upstream_curr_rate",
            device_downstream_max_bit_rate = "livebox_device_downstream_max_bit_rate",
            device_upstream_max_bit_rate = "livebox_device_upstream_max_bit_rate",
            ont_rx_packets = "livebox_ont_rx_packets",
            ont_tx_packets = "livebox_ont_tx_packets",
            ont_rx_bytes = "livebox_ont_rx_bytes",
            ont_tx_bytes = "livebox_ont_tx_bytes",
            ont_rx_errors = "livebox_ont_rx_errors",
            ont_tx_errors = "livebox_ont_tx_errors",
            ont_rx_dropped = "livebox_ont_rx_dropped",
            ont_tx_dropped = "livebox_ont_tx_dropped",
            ont_multicast = "livebox_ont_multicast",
            ont_collisions = "livebox_ont_collisions",
            ont_rx_length_errors = "livebox_ont_rx_length_errors",
            ont_rx_over_errors = "livebox_ont_rx_over_errors",
            ont_rx_crc_errors = "livebox_ont_rx_crc_errors",
            ont_rx_frame_errors = "livebox_ont_rx_frame_errors",
            ont_rx_fifo_errors = "livebox_ont_rx_fifo_errors",
            ont_rx_missed_errors = "livebox_ont_rx_missed_errors",
            ont_tx_aborted_errors = "livebox_ont_tx_aborted_errors",
            ont_tx_carrier_errors = "livebox_ont_tx_carrier_errors",
            ont_tx_fifo_errors = "livebox_ont_tx_fifo_errors",
            ont_tx_heartbeat_errors = "livebox_ont_tx_heartbeat_errors",
            ont_tx_window_errors = "livebox_ont_tx_window_errors",
            ont_rx_bit_rate = "livebox_ont_rx_bit_rate",
            ont_tx_bit_rate = "livebox_ont_tx_bit_rate",
            nmc_wan_state = "livebox_nmc_wan_state",
            nmc_link_state = "livebox_nmc_link_state",
            nmc_gpon_state = "livebox_nmc_gpon_state",
            nmc_connection_state = "livebox_nmc_connection_state";

        public LB5Metrics()
        {
            LiveboxMetricDescriptor[] descriptors = Resources.GetLB5MetricDescriptors();
            int numberOfCounters = descriptors.Count(t => t.Type == LiveboxMetricDescriptor.CollectorType.Counter);
            int numberOfGauges = descriptors.Count(t => t.Type == LiveboxMetricDescriptor.CollectorType.Gauge);

            _counters = new Dictionary<string, Counter>(numberOfCounters);
            _gauges = new Dictionary<string, Gauge>(numberOfGauges);
            var gaugeConfig = new GaugeConfiguration { SuppressInitialValue = true };
            var counterConfig = new CounterConfiguration { SuppressInitialValue = true };
            foreach (LiveboxMetricDescriptor descriptor in descriptors)
            {
                bool duplicated;
                switch (descriptor.Type)
                {
                    case LiveboxMetricDescriptor.CollectorType.Counter:
                        duplicated = !_counters.TryAdd(descriptor.Name, Metrics.CreateCounter(descriptor.Name, descriptor.Description, counterConfig));
                        break;
                    case LiveboxMetricDescriptor.CollectorType.Gauge:
                        duplicated = !_gauges.TryAdd(descriptor.Name, Metrics.CreateGauge(descriptor.Name, descriptor.Description, gaugeConfig));
                        break;
                    default:
                        throw new NotSupportedException($"Type {descriptor.Type} is not expected.");
                }

                if (duplicated)
                    throw new InvalidOperationException($"Name '{descriptor.Name}' is duplicated.");
            }
        }

        public void Update(IReadOnlyDictionary<string, long> values)
        {
            foreach (KeyValuePair<string, long> kvp in values)
            {
                if (_gauges.TryGetValue(kvp.Key, out var gauge))
                {
                    gauge.Set(kvp.Value);
                }
                else if (_counters.TryGetValue(kvp.Key, out var counter))
                {
                    counter.IncTo(kvp.Value);
                }
            }
        }
    }
}
