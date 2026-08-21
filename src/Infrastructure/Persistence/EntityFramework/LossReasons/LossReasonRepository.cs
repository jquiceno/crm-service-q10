using Infrastructure.Persistence.EntityFramework.Common;
using Infrastructure.Persistence.EntityFramework.LossReasons.Mappers;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using LossReason.Domain.Queries;
using LossReason.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.LossReasons;

/// <summary>
/// Persists the loss reason aggregate against the legacy <c>tbl_opo_causas</c> table.
/// </summary>
/// <remarks>
/// Implements the domain contract directly: <c>RepositoryBaseEF</c> assumes the aggregate is the
/// mapped entity, and here they are two different types. No method lets an exception escape.
/// </remarks>
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
                .FirstOrDefaultAsync(x => x.CauConsecutivoP == id, cancellationToken)
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
                .AnyAsync(x => x.CauConsecutivoP == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of loss reason with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>
    /// Delegates to <see cref="GetAsync"/> with an empty filter: the unfiltered listing is the
    /// filtered one without criteria, and duplicating the query would let the two orderings diverge.
    /// </summary>
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
                query = query.Where(x => x.CauNombre != null && x.CauNombre.Contains(name));

            if (filter.IsActive.HasValue)
                query = query.Where(x => x.CauEstado == filter.IsActive.Value);

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            // The tie-break by the key is mandatory: cau_nombre is neither unique nor indexed, and
            // OFFSET/FETCH can repeat or skip rows between pages when the ordering is not unique.
            var documents = await query
                .OrderBy(x => x.CauNombre)
                .ThenBy(x => x.CauConsecutivoP)
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

    /// <summary>
    /// Persists the insert and returns the aggregate carrying the identity value the database
    /// assigned. A use case that creates through here does not commit: the commit already happened.
    /// </summary>
    public async Task<Result<LossReasonAggregate>> CreateAsync(
        LossReasonAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = LossReasonRepositoryMapper.ToDocument(aggregate);

            await _lossReasons.AddAsync(document, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // cau_consecutivoP is populated after SaveChanges; the aggregate is rebuilt from the row
            // because the identity value cannot be assigned onto an existing aggregate.
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

            // The mapper leaves the identity column untouched because it is not written on insert;
            // an update does need it to address the existing row.
            document.CauConsecutivoP = aggregate.Id;

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
            // Tracked on purpose: the delete is staged here and committed by the unit of work.
            var document = await _lossReasons
                .FirstOrDefaultAsync(x => x.CauConsecutivoP == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return LossReasonErrors.NotFound(id) with { Origin = Origin };

            _lossReasons.Remove(document);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing loss reason with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
