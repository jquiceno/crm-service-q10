using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.DeleteBusinessStatus;

public sealed class DeleteBusinessStatusUseCase(
    IBusinessStatusRepository repository,
    IUnitOfWorkPort unitOfWork) : IDeleteBusinessStatusUseCase
{
    private const string Origin = nameof(DeleteBusinessStatusUseCase);

    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        // The aggregate is loaded not to confirm existence — RemoveAsync already answers NotFound —
        // but because the terminal guard is a rule of the aggregate, and only the aggregate can tell
        // whether the row sitting at 0 % or 100 % is a terminal status (INV-3).
        var aggregateResult = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (aggregateResult.IsFailure)
            return aggregateResult.Error;

        var deletableResult = aggregateResult.Value.EnsureCanBeDeleted();
        if (deletableResult.IsFailure)
            return deletableResult.Error with { Context = BusinessStatusErrors.Context, Origin = Origin };

        var removeResult = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        if (removeResult.IsFailure)
            return removeResult.Error;

        // The 409 raised by the incoming foreign keys of tbl_opo_negocios arrives here already
        // classified by the Unit of Work, and is propagated without being rewritten.
        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
