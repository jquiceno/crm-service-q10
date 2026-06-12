namespace ServiceInfo.Application.UseCases.GetServiceInfo;

public sealed record GetServiceInfoOutputDto(string Status, string Name, string Version, string TemplateVersion);
