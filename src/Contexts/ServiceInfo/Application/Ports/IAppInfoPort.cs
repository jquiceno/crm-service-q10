namespace ServiceInfo.Application.Ports;

public interface IAppInfoPort
{
    string ServiceName { get; }
    string Version { get; }
}
