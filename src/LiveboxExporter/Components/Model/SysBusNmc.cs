namespace LiveboxExporter.Components.Model
{
    public class SysBusNmc
    {
        public bool Status { get; set; }
        public SysBusNmcData Data { get; set; } = default!;

        public class SysBusNmcData
        {
            public string WanState { get; set; } = default!;
            public string LinkState { get; set; } = default!;
            public string GponState { get; set; } = default!;
            public string ConnectionState { get; set; } = default!;
        }
    }
}
