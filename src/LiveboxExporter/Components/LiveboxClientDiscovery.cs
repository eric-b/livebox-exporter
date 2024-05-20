using Newtonsoft.Json;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using LiveboxExporter.Utility;
using System.Runtime.CompilerServices;

namespace LiveboxExporter.Components
{
    public readonly record struct LiveboxDiscoveryResult(Uri? address,
                                                         string? productClass,
                                                         string? serialNumber,
                                                         string? softwareVersion,
                                                         string? baseMac,
                                                         IReadOnlyDictionary<string, string> other);

    /// <summary>
    /// Best effort to discover Livebox. Should work
    /// if it's located in same subnet.
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="logger"></param>
    public sealed class LiveboxClientDiscovery(HttpClient httpClient, ILogger<LiveboxClientDiscovery> logger)
    {
        private static readonly Uri[] DefaultGatewayAddresses =
            [
                new Uri("http://192.168.1.1/"),
                new Uri("http://livebox.home/")
            ];

        public async Task<LiveboxDiscoveryResult> TryDiscoverLiveboxAddress(CancellationToken cancellationToken)
        {
            var tracertResult = TraceRoute(IPAddress.Parse("8.8.8.8"), cancellationToken).ConfigureAwait(false);
            await foreach (IPAddress ipAddress in tracertResult.ConfigureAwait(false))
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork &&
                    IpAddressRange.IsPrivateAddress(ipAddress))
                {
                    var address = new Uri($"http://{ipAddress}/");
                    var result = await ProbeLivebox(address, cancellationToken).ConfigureAwait(false);
                    if (result.address != null)
                    {
                        return result;
                    }
                }
            }

            foreach (Uri item in DefaultGatewayAddresses)
            {
                var result = await ProbeLivebox(item, cancellationToken).ConfigureAwait(false);
                if (result.address != null)
                {
                    return result;
                }
            }

            return default;
        }

        private async Task<LiveboxDiscoveryResult> ProbeLivebox(Uri baseAddress, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = LiveboxClient.CreateWsRequest(new LiveboxClient.SaHRequest("DeviceInfo", "get", new Dictionary<string, object>()), baseAddress))
                {
                    using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.OK &&
                            response.Content != null &&
                            response.Content.Headers.ContentType?.MediaType?.EndsWith("json") == true)
                        {
                            var deviceInfoResponseContent = new
                            {
                                status = new Dictionary<string, string>()
                            };
                            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            var info = JsonConvert.DeserializeAnonymousType(json, deviceInfoResponseContent);
                            if (info?.status?.Count > 0)
                            {
                                info.status.TryGetValue("ProductClass", out string? productClass);
                                info.status.TryGetValue("SerialNumber", out string? serial);
                                info.status.TryGetValue("SoftwareVersion", out string? version);
                                info.status.TryGetValue("BaseMAC", out string? baseMac);
                                var filter = new string[]
                                {
                                    "ProductClass",
                                    "SerialNumber",
                                    "SoftwareVersion",
                                    "BaseMAC"
                                };
                                logger.LogInformation($"Discovery success: {baseMac} {baseAddress} - {productClass} {serial} {version}");
                                return new LiveboxDiscoveryResult(baseAddress,
                                                                  productClass,
                                                                  serial,
                                                                  version,
                                                                  baseMac,
                                                                  info.status.Where(s => !filter.Contains(s.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, $"Failed to probe Livebox on {baseAddress}.");
                }
            }
            return default;
        }

        private static async IAsyncEnumerable<IPAddress> TraceRoute(IPAddress target, [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            // Src: https://stackoverflow.com/a/45565253/249742
            TimeSpan timeout = TimeSpan.FromMilliseconds(10000);
            const int maxTTL = 30;
            const int bufferSize = 32;

            byte[] buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);

            using (var pinger = new Ping())
            {
                for (int ttl = 1; ttl <= maxTTL; ttl++)
                {
                    PingOptions options = new PingOptions(ttl, dontFragment: true);
                    PingReply reply = await pinger.SendPingAsync(target, timeout, buffer, options, cancellationToken).ConfigureAwait(false);

                    yield return reply.Address;

                    if (reply.Status != IPStatus.TtlExpired && reply.Status != IPStatus.TimedOut)
                        break;
                }
            }
        }
    }
}
