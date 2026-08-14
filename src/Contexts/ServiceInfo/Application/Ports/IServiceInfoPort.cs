namespace ServiceInfo.Application.Ports;

public interface IServiceInfoPort
{
    string Name { get; }
    string Version { get; }
    string TemplateVersion { get; }
}
