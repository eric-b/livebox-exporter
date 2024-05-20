
namespace LiveboxExporter.Components.Model
{
    public class SysBusDevice : IWithError
    {
        public SysBusDeviceStatus? Status { get; set; }

        public ResultError[]? Errors { get; set; }

        public class SysBusDeviceStatus
        {
            public bool Active { get; set; }
            public string LinkState { get; set; } = default!;
            public string ConnectionState { get; set; } = default!;
            public bool Internet { get; set; }
            public bool IPTV { get; set; }
            public bool Telephony { get; set; }
            public int DownstreamCurrRate { get; set; }
            public int UpstreamCurrRate { get; set; }
            public int DownstreamMaxBitRate { get; set; }
            public int UpstreamMaxBitRate { get; set; }
        }
    }
    

}
