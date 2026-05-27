using Health.Application.Ports;
using Shared.Results;

namespace Health.Application.UseCases.GetHealthInfo;

public sealed class GetHealthInfoUseCase(IAppInfoPort appInfo) : IGetHealthInfoPort
{
    public Task<Result<GetHealthInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var output = new GetHealthInfoOutputDto("ok", appInfo.ServiceName, appInfo.Version);
        return Task.FromResult(Result<GetHealthInfoOutputDto>.Success(output));
    }
}
