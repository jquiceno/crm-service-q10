using Health.Application.UseCases.GetHealthInfo;
using Shared.Result;

namespace Health.Application.Ports;

public interface IGetHealthInfoPort
{
    Task<Result<GetHealthInfoOutputDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
