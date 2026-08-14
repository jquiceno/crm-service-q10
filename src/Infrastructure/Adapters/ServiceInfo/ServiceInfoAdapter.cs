using ServiceInfo.Application.Ports;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.ServiceInfo;

public sealed class ServiceInfoAdapter(
    IOptions<ServiceInfoSettings> serviceInfo,
    IOptions<TemplateSettings> templateInfo) : IServiceInfoPort
{
    public string Name => serviceInfo.Value.Name;
    public string Version => serviceInfo.Value.Version;
    public string TemplateVersion => templateInfo.Value.Version;
}
