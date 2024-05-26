using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net;
using LiveboxExporter.Components.Model;

namespace LiveboxExporter.Components
{
    public sealed class LiveboxAuthorizationHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        private readonly string? _createContextJsonInput;
        private string? _contextId;
        private DateTimeOffset? _disableAuthUntil;

        public static readonly HttpRequestOptionsKey<bool> ForceAuthOptionKey = new HttpRequestOptionsKey<bool>("force-auth");

        public LiveboxAuthorizationHandler(IOptions<LiveboxAuthorizationHandlerOptions> options, ILogger<LiveboxAuthorizationHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            string? pwd = TryGetPassword(options.Value);
            if (pwd is not null)
            {
                _createContextJsonInput = JsonConvert.SerializeObject(new LiveboxClient.SaHRequest("sah.Device.Information", "createContext", new Dictionary<string, object>
                {
                    { "applicationName", "webui"},
                    { "username", "admin"},
                    { "password", pwd }
                }));
            }
            else
            {
                logger.LogWarning("Livebox admin password was not provided in application settings: some metrics will be missing.");
            }   
        }

        private static string? TryGetPassword(LiveboxAuthorizationHandlerOptions options)
        {
            if (!string.IsNullOrEmpty(options.PasswordFile))
            {
                if (!File.Exists(options.PasswordFile))
                    throw new FileNotFoundException($"File not found or not accessible: '{options.PasswordFile}'.", options.PasswordFile);
                return Nito.AsyncEx.AsyncContext.Run(() => File.ReadAllTextAsync(options.PasswordFile));
            }
            return string.IsNullOrEmpty(options.Password) ? null : options.Password;
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null && _createContextJsonInput is null)
            {
                if (_disableAuthUntil is null || _disableAuthUntil.Value < TimeProvider.System.GetUtcNow())
                {
                    _disableAuthUntil = null;
                    string? contextId = _contextId;
                    if (contextId is null || ShouldForceAuth(request.Options))
                    {
                        contextId = CreateContext(new Uri($"http://{request.RequestUri!.Authority}/"), cancellationToken);
                    }

                    if (contextId != null)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("X-Sah", _contextId);
                    }
                }
                else
                {
                    _logger.LogInformation("Auth is disabled until {Time}", _disableAuthUntil);
                }
            }

            try
            {
                return base.Send(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (_contextId != null)
                {
                    // Forget contextId in case of connectivity issue: if livebox reboot, it will not be fully recognized.
                    _contextId = null;
                    _logger.LogWarning(ex, "Authentication context cleared because of connectivity issue with Livebox.");
                }
                throw;
            }
        }

        private bool ShouldForceAuth(HttpRequestOptions requestOptions)
        {
            return requestOptions.TryGetValue(ForceAuthOptionKey, out bool flag) && flag;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null && _createContextJsonInput != null)
            {
                if (_disableAuthUntil is null || _disableAuthUntil.Value < TimeProvider.System.GetUtcNow())
                {
                    _disableAuthUntil = null;
                    string? contextId = _contextId;
                    if (contextId is null || ShouldForceAuth(request.Options))
                    {
                        contextId = await CreateContextAsync(new Uri($"http://{request.RequestUri!.Authority}/"), cancellationToken).ConfigureAwait(false);
                    }

                    if (contextId != null)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("X-Sah", _contextId);
                    }
                }
                else
                {
                    _logger.LogInformation("Auth is disabled until {Time}", _disableAuthUntil);
                }
            }

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (_contextId != null)
                {
                    // Forget contextId in case of connectivity issue: if livebox reboot, it will not be fully recognized.
                    _contextId = null;
                    _logger.LogWarning(ex, "Authentication context cleared because of connectivity issue with Livebox.");
                }
                throw;
            }
        }

        private HttpRequestMessage CreateLoginRequest(Uri baseAddress)
        {
            if (_createContextJsonInput is null)
                throw new InvalidOperationException();
            var request = LiveboxClient.CreateWsRequest(_createContextJsonInput, baseAddress);
            request.Headers.Authorization = new AuthenticationHeaderValue("X-Sah-Login");
            return request;
        }

        private string CreateContext(Uri baseAddress, CancellationToken cancellationToken)
        {
            using var request = CreateLoginRequest(baseAddress);
            using var response = base.Send(request, cancellationToken);
            string? responseJson = null;
            if (response.Content != null &&
                response.Content.Headers.ContentType?.MediaType?.EndsWith("json") == true)
            {
                using Stream responseStream = response.Content.ReadAsStream(cancellationToken);
                using var reader = new StreamReader(responseStream);
                responseJson = reader.ReadToEnd();
                var result = JsonConvert.DeserializeObject<AuthResponse>(responseJson);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (result?.Data?.ContextID != null)
                    {
                        _logger.LogInformation("New authentication context created.");
                        _contextId = result.Data.ContextID;
                        _disableAuthUntil = null;
                        return _contextId;
                    }
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized && result?.Errors?[0]?.Waittime > 0)
                {
                    _disableAuthUntil = TimeProvider.System.GetUtcNow().AddSeconds(result.Errors[0].Waittime + 1);
                }
            }
            throw new Exception($"Failed to create session context (authentication). Response: {response.StatusCode} - {response.Content?.Headers.ContentType} {responseJson}");
        }

        private async Task<string> CreateContextAsync(Uri baseAddress, CancellationToken cancellationToken)
        {
            using var request = CreateLoginRequest(baseAddress);
            using var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string? responseJson = null;
            try
            {
                if (response.Content != null &&
                    response.Content.Headers.ContentType?.MediaType?.EndsWith("json") == true)
                {
                    using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(responseStream);
                    responseJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var result = JsonConvert.DeserializeObject<AuthResponse>(responseJson);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (result?.Data?.ContextID != null)
                        {
                            _logger.LogInformation("New authentication context created.");
                            _contextId = result.Data.ContextID;
                            _disableAuthUntil = null;
                            return _contextId;
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized && result?.Errors?[0]?.Waittime > 0)
                    {
                        _disableAuthUntil = TimeProvider.System.GetUtcNow().AddSeconds(result.Errors[0].Waittime + 1);
                    }
                }
            }
            catch
            {
                // eg: Response: OK - application/x-sah-ws-4-call+json; charset=UTF-8 {"status":null,"errors":[{"error":13,"description":"Permission denied","info":"sah.Device.Information"}]}
                throw new Exception($"Failed to create session context (authentication). Response: {response.StatusCode} - {response.Content?.Headers.ContentType} {responseJson}");
            }
            throw new Exception($"Failed to create session context (authentication). Response: {response.StatusCode} - {response.Content?.Headers.ContentType} {responseJson}");
        }
    }
}
