namespace LiveboxExporter.Components.Model
{
    public sealed class NeMo
    {
        public NeMoStatus Status { get; set; }

        public sealed class NeMoStatus
        {
            public bool Enable { get; set; }
            public bool Status { get; set; }
            public int TxQueueLen { get; set; }
            public int MTU { get; set; }
            public string NetDevState { get; set; }
            public int SignalRxPower { get; set; }
            public int SignalTxPower { get; set; }
            public int Temperature { get; set; }
            public int Voltage { get; set; }
            public int Bias { get; set; }
            public string ONUState { get; set; }
            public int DownstreamMaxRate { get; set; }
            public int UpstreamMaxRate { get; set; }
            public int DownstreamCurrRate { get; set; }
            public int UpstreamCurrRate { get; set; }
        }
    }
}
