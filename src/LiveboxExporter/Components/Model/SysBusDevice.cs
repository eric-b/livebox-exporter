namespace LiveboxExporter.Components.Model
{
    public class SysBusDevice
    {
        public Status? status { get; set; }

        public Error[]? errors { get; set; }

        public class Status
        {
            public bool Active { get; set; }
            public string LinkState { get; set; }
            public string ConnectionState { get; set; }
            public bool Internet { get; set; }
            public bool IPTV { get; set; }
            public bool Telephony { get; set; }
            public int DownstreamCurrRate { get; set; }
            public int UpstreamCurrRate { get; set; }
            public int DownstreamMaxBitRate { get; set; }
            public int UpstreamMaxBitRate { get; set; }
        }

        public class Error
        {
            public int error { get; set; }
            public string description { get; set; }
            public string info { get; set; }
        }
    }
    

}
