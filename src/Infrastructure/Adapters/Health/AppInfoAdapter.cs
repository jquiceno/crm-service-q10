using Health.Application.Ports;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Health;

public sealed class AppInfoAdapter(IOptions<AppInfoSettings> options) : IAppInfoPort
{
    public string ServiceName => options.Value.ServiceName;
    public string Version => options.Value.Version;
}
