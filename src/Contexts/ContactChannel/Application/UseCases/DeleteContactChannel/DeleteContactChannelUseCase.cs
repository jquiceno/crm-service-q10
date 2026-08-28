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
        var existsResult = await repository.ExistsAsync(id, cancellationToken).ConfigureAwait(false);

        if (existsResult.IsFailure)
            return existsResult.Error;

        if (!existsResult.Value)
            return ContactChannelErrors.NotFound(id) with { Origin = Origin };

        var usageResult = await usageReader.IsReferencedAsync(id, cancellationToken).ConfigureAwait(false);

        if (usageResult.IsFailure)
            return usageResult.Error;

        if (usageResult.Value)
            return ContactChannelErrors.InUse(id) with { Origin = Origin };

        var removeResult = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);

        if (removeResult.IsFailure)
            return removeResult.Error;

        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
