using ContactChannel.Application.Ports;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace ContactChannel.Application.UseCases.DeleteContactChannel;

public sealed class DeleteContactChannelUseCase(
    IContactChannelRepository repository,
    IContactChannelUsageReader usageReader,
    IUnitOfWorkPort unitOfWork) : IDeleteContactChannelUseCase
{
    private const string Origin = nameof(DeleteContactChannelUseCase);

    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var usageResult = await usageReader.IsReferencedAsync(id, cancellationToken).ConfigureAwait(false);

        if (usageResult.IsFailure)
            return usageResult.Error;

        if (usageResult.Value)
            return ContactChannelErrors.InUse(id) with { Origin = Origin };

        var deleteResult = await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (deleteResult.IsFailure)
            return deleteResult.Error;

        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
