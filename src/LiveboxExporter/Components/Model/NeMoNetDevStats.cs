using System.Diagnostics;

namespace LiveboxExporter.Components.Model
{

    public sealed class NeMoNetDevStats : IWithError
    {
        public NeMoNetDevStatsStatus? Status { get; set; }

        public ResultError[]? Errors { get; set; }

        public sealed class NeMoNetDevStatsStatus
        {
            private readonly long _timestamp = Stopwatch.GetTimestamp();

            public TimeSpan GetElapsedTimeSince(NeMoNetDevStatsStatus other)
            {
                return Stopwatch.GetElapsedTime(other._timestamp, _timestamp);
            }

            public long RxPackets { get; set; }
            public long TxPackets { get; set; }
            public long RxBytes { get; set; }
            public long TxBytes { get; set; }
            public long RxErrors { get; set; }
            public long TxErrors { get; set; }
            public long RxDropped { get; set; }
            public long TxDropped { get; set; }
            public long Multicast { get; set; }
            public long Collisions { get; set; }
            public long RxLengthErrors { get; set; }
            public long RxOverErrors { get; set; }
            public long RxCrcErrors { get; set; }
            public long RxFrameErrors { get; set; }
            public long RxFifoErrors { get; set; }
            public long RxMissedErrors { get; set; }
            public long TxAbortedErrors { get; set; }
            public long TxCarrierErrors { get; set; }
            public long TxFifoErrors { get; set; }
            public long TxHeartbeatErrors { get; set; }
            public long TxWindowErrors { get; set; }
        }
    }
}
