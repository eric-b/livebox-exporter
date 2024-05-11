namespace LiveboxExporter.Components.Model
{
    public class SysBusNmc
    {
        public bool status { get; set; }
        public Data data { get; set; }

        public class Data
        {
            public string WanState { get; set; }
            public string LinkState { get; set; }
            public string GponState { get; set; }
            public string ConnectionState { get; set; }
        }
    }
}
