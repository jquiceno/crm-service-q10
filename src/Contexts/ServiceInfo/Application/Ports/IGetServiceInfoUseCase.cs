using ServiceInfo.Application.UseCases.GetServiceInfo;
using Shared.Results;

namespace ServiceInfo.Application.Ports;

public interface IGetServiceInfoUseCase
{
    Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
