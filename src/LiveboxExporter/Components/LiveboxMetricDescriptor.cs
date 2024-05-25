using System.Diagnostics;

namespace LiveboxExporter.Components
{
    [DebuggerDisplay("{Name} ({Type})")]
    public sealed class LiveboxMetricDescriptor
    {
        public string Name { get; }

        public CollectorType Type { get; }

        public string Description { get; }

        public enum CollectorType
        {
            Gauge,
            Counter
        }

        public LiveboxMetricDescriptor(string name, CollectorType type, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}
