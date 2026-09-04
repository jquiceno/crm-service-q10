using ServiceInfo.Application.Ports;
using Shared.Results;

namespace ServiceInfo.Application.UseCases.GetServiceInfo;

public sealed class GetServiceInfoUseCase(IServiceInfoPort serviceInfo) : IGetServiceInfoUseCase
{
    public Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var output = new GetServiceInfoOutputDto("ok", serviceInfo.Name, serviceInfo.Version, serviceInfo.TemplateVersion);
        return Task.FromResult(Result<GetServiceInfoOutputDto>.Success(output));
    }
}
