using Shared.Results;

namespace ServiceInfo.Application.UseCases.GetServiceInfo;

public interface IGetServiceInfoUseCase
{
    Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
