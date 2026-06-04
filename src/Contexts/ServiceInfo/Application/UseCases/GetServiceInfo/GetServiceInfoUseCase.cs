using ServiceInfo.Application.Ports;
using Shared.Results;

namespace ServiceInfo.Application.UseCases.GetServiceInfo;

public sealed class GetServiceInfoUseCase(IAppInfoPort appInfo) : IGetServiceInfoPort
{
    public Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var output = new GetServiceInfoOutputDto("ok", appInfo.ServiceName, appInfo.Version);
        return Task.FromResult(Result<GetServiceInfoOutputDto>.Success(output));
    }
}
