using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public sealed class UpdateContactChannelUseCase(
    IContactChannelRepository repository,
    IUnitOfWorkPort unitOfWork) : IUpdateContactChannelUseCase
{
    private const string Origin = nameof(UpdateContactChannelUseCase);

    public async Task<Result<UpdateContactChannelOutputDto>> ExecuteAsync(
        int id,
        UpdateContactChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        var channelResult = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (channelResult.IsFailure)
            return channelResult.Error;

        var channel = channelResult.Value;
        var updateResult = channel.Update(input.ToUpdateArgs());

        if (updateResult.IsFailure)
            return updateResult.Error with { Context = ContactChannelErrors.Context, Origin = Origin };

        var persistResult = repository.Update(channel);

        if (persistResult.IsFailure)
            return persistResult.Error;

        var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (commitResult.IsFailure)
            return commitResult.Error;

        return channel.ToOutputDto();
    }
}
