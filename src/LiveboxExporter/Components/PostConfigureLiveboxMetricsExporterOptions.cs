using Microsoft.Extensions.Options;

namespace LiveboxExporter.Components
{
    public class PostConfigureLiveboxMetricsExporterOptions(IOptions<LiveboxAuthorizationHandlerOptions> handlerOptions) : IPostConfigureOptions<LiveboxMetricsExporterOptions>
    {
        public void PostConfigure(string? name, LiveboxMetricsExporterOptions options)
        {
            if (name == Options.DefaultName)
            {
                options.AuthIsDisabled = 
                    string.IsNullOrEmpty(handlerOptions.Value.Password) && 
                    string.IsNullOrEmpty(handlerOptions.Value.PasswordFile);
            }
        }
    }
}
