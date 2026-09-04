using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.Common;
using Infrastructure.Persistence.EntityFramework.LossReasons.Mappers;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using LossReason.Domain.Queries;
using LossReason.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.LossReasons;

public sealed class LossReasonRepository(
    ApplicationDbContext context,
    ILoggerPort<LossReasonRepository> logger) : ILossReasonRepository
{
    private const string Origin = nameof(LossReasonRepository);

    private readonly DbSet<Entities.LossReason> _lossReasons = context.Set<Entities.LossReason>();

    public async Task<Result<LossReasonAggregate>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _lossReasons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return LossReasonErrors.NotFound(id) with { Origin = Origin };

            return LossReasonRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving loss reason with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _lossReasons
                .AsNoTracking()
                .AnyAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of loss reason with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Task<PagedResult<LossReasonAggregate>> GetAllAsync(
        PageQuery page,
        CancellationToken cancellationToken = default) =>
        GetAsync(new LossReasonFilter(Name: null, IsActive: null), page, cancellationToken);

    public async Task<PagedResult<LossReasonAggregate>> GetAsync(
        LossReasonFilter filter,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _lossReasons.AsNoTracking();

            var name = filter.Name;
            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(x => x.Name.Contains(name));

            if (filter.IsActive.HasValue)
                query = query.Where(x => x.IsActive == filter.IsActive.Value);

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            // The tie-break by the key is mandatory: the name is neither unique nor indexed, and
            // OFFSET/FETCH can repeat or skip rows between pages when the ordering is not unique.
            var documents = await query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<LossReasonAggregate> aggregates =
                [.. documents.Select(LossReasonRepositoryMapper.ToDomain)];

            return PagedResult<LossReasonAggregate>.Success(aggregates, totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving loss reasons");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<LossReasonAggregate>> CreateAsync(
        LossReasonAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = LossReasonRepositoryMapper.ToDocument(aggregate);

            await _lossReasons.AddAsync(document, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Rebuilt from the row because the generated key cannot be assigned onto the aggregate.
            return LossReasonRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error inserting loss reason");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> AddAsync(LossReasonAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = LossReasonRepositoryMapper.ToDocument(aggregate);

            await _lossReasons.AddAsync(document, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error adding loss reason");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Result Update(LossReasonAggregate aggregate)
    {
        try
        {
            var document = LossReasonRepositoryMapper.ToDocument(aggregate);

            // The mapper leaves the key unset for inserts; an update needs it to address the row.
            document.Id = aggregate.Id;

            _lossReasons.Update(document);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error updating loss reason with id {Id}", aggregate.Id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            // A single DELETE, without loading the row first: existence is not checked here. An id
            // that is not there affects zero rows, which is a success — the delete is idempotent.
            await _lossReasons
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (SqlException ex)
        {
            // The DELETE no longer passes through the unit of work, so the 547 of a race lost
            // against a deal that took the reason has to be classified here to stay a 409 (§6.5).
            logger.Error(ex, "Error removing loss reason with id {Id}", id);
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing loss reason with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
