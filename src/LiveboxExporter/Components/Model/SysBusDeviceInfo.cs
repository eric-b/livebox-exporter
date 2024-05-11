namespace LiveboxExporter.Components.Model
{
    public class SysBusDeviceInfo
    {
        public Status status { get; set; }

        public class Status
        {
            public int? UpTime { get; set; }
            public string? DeviceStatus { get; set; }
            public int? NumberOfReboots { get; set; }
            public string BaseMAC { get; set; }
        }
    }    
}
