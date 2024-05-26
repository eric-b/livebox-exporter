using LiveboxExporter.Components;
using System.Reflection;

namespace LiveboxExporter
{
    static class Resources
    {
        static readonly Assembly ThisAssembly = Assembly.GetExecutingAssembly();

        static readonly string Namespace = typeof(Resources).Namespace + "." + nameof(Resources) + ".";

        static readonly string LB5Metrics = Namespace + "LB5-metrics.csv";

        public static LiveboxMetricDescriptor[] GetLB5MetricDescriptors()
        {
            var result = new List<LiveboxMetricDescriptor>();
            using (var stream = OpenLB5Metrics())
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null
                    && !string.IsNullOrEmpty(line))
                {
                    const int indexNotFound = -1;
                    int indexType, indexDesc;
                    indexType = line.IndexOf(';');
                    if (indexType == indexNotFound)
                        throw new InvalidOperationException($"Failed to parse line: '{line}'.");

                    string name = line.Substring(0, indexType);

                    indexDesc = line.IndexOf(';', indexType + 1);
                    if (indexDesc == indexNotFound || indexDesc + 1 >= line.Length)
                        throw new InvalidOperationException($"Failed to parse line: '{line}'.");

                    string type = line.Substring(indexType + 1, indexDesc - (indexType + 1));
                    string desc = line.Substring(indexDesc + 1);

                    if (!Enum.TryParse<LiveboxMetricDescriptor.CollectorType>(type, true, out var typeCategory))
                        throw new InvalidOperationException($"Failed to parse line: '{line}'.");

                    result.Add(new LiveboxMetricDescriptor(name, typeCategory, desc));
                }
            }
            return result.ToArray();
        }

        private static Stream OpenLB5Metrics() 
            => OpenResource(LB5Metrics);

        private static Stream OpenResource(string path)
        {
            Stream? stream = ThisAssembly.GetManifestResourceStream(path);
            if (stream is null)
                throw new ArgumentOutOfRangeException($"Embedded resource not found: '{path}'.");

            return stream;
        }
    }
}
