namespace LiveboxExporter.Components.Model
{
    public class SysBusDeviceInfo
    {
        public SysBusDeviceInfoStatus Status { get; set; } = default!;

        public class SysBusDeviceInfoStatus
        {
            public int? UpTime { get; set; }
            public string? DeviceStatus { get; set; }
            public int? NumberOfReboots { get; set; }
            public string BaseMAC { get; set; } = default!;
        }
    }    
}
