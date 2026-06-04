using ServiceInfo.Application.UseCases.GetServiceInfo;
using Shared.Results;

namespace ServiceInfo.Application.Ports;

public interface IGetServiceInfoPort
{
    Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
