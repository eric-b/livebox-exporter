namespace LiveboxExporter.Components
{
    public sealed class LiveboxMetricsBackgroundWorkerOptions
    {
        public static readonly TimeSpan DefaultTimerInterval = TimeSpan.FromSeconds(10); 

        public TimeSpan? TimerInterval { get; set; }
    }
}
