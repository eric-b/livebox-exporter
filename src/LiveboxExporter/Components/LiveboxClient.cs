using System.Net.Http.Headers;
using System.Net;

namespace LiveboxExporter.Components
{
    public sealed class LiveboxClient
    {
        internal static readonly MediaTypeHeaderValue SahJsonContentType = new MediaTypeHeaderValue("application/x-sah-ws-4-call+json");
        internal record SaHRequest(string service, string method, Dictionary<string, object> parameters);
        internal record SaHResponse(HttpStatusCode status, string? json);

        private readonly HttpClient _httpClient;
        private bool _forceAuthOnNextRequest;

        readonly Uri _wsBaseAddress;
        static readonly Uri WsRelativeUrl = new Uri("ws", UriKind.Relative);

        public LiveboxClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            if (httpClient.BaseAddress is null)
                throw new ArgumentException("A base address must be provided.", nameof(httpClient.BaseAddress));
            _wsBaseAddress = new Uri(httpClient.BaseAddress, WsRelativeUrl);
        }

        public void ForceAuthOnNextRequest() => _forceAuthOnNextRequest = true;

        public async Task<string?> RawCallFunctionWithoutParameter(string service, string method, CancellationToken cancellationToken)
        {
            using var request = CreateWsRequest(new SaHRequest(service, method, new Dictionary<string, object>()));
            if (_forceAuthOnNextRequest)
            {
                request.Options.Set(LiveboxAuthorizationHandler.ForceAuthOptionKey, true);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string? responseJson = null;
            if (response.StatusCode == HttpStatusCode.OK &&
                response.Content != null &&
                response.Content.Headers.ContentType?.MediaType?.EndsWith("json") == true)
            {
                using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(responseStream);
                responseJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                _forceAuthOnNextRequest = false;
                return responseJson;
            }
            return null;
        }

        internal HttpRequestMessage CreateWsRequest<T>(T payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _wsBaseAddress);
            request.Content = JsonContent.Create(payload, SahJsonContentType);
            return request;
        }

        internal static HttpRequestMessage CreateWsRequest<T>(T payload, Uri? baseAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, baseAddress is null ? WsRelativeUrl : new Uri(baseAddress, WsRelativeUrl));
            request.Content = JsonContent.Create(payload, SahJsonContentType);
            return request;
        }

        internal static HttpRequestMessage CreateWsRequest(string payloadJson, Uri? baseAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, baseAddress is null ? WsRelativeUrl : new Uri(baseAddress, WsRelativeUrl));
            request.Content = new StringContent(payloadJson, SahJsonContentType);
            return request;
        }
    }
}
