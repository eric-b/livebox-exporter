namespace LiveboxExporter.Extensions.Dictionary
{
    static class DictionaryExtensions
    {
        public static void SetZero(this Dictionary<string, long> dic, params string[] keys)
        {
            foreach (string key in keys)
            {
                dic[key] = 0;
            }
        }
    }
}
