using Activities.Domain.Aggregates;
using Activities.Domain.Errors;
using Activities.Domain.Filters;
using Activities.Domain.Repositories;
using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Infrastructure.Persistence.EntityFramework.Activities.Mappers;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// EF Core persistence for the <see cref="Activity"/> aggregate.
/// </summary>
/// <remarks>
/// Cannot inherit <see cref="RepositoryBaseEF{TAggregate, TId}"/>: that base assumes the aggregate
/// itself is the type EF maps, so saving it is enough for EF to hand the generated id back on the
/// very same object. Here it is not — <see cref="ActivityEntity"/> is the mapped shape (F2.2), a
/// separate object translated in both directions by <see cref="ActivityRepositoryMapper"/> — so
/// every operation below reimplements what the base would otherwise provide, in the same
/// try/catch/logger shape.
/// <para>
/// <see cref="AddAsync"/> is the one exception to "no repository saves on its own": the id is a
/// SQL Server <c>IDENTITY</c>, unknown until the insert actually runs, and nothing links the
/// discarded <see cref="ActivityEntity"/> back to the caller's <see cref="Activity"/> once
/// <c>AddAsync</c> returns — so there is no later point at which the id could still be copied
/// over. It saves immediately and assigns the id right there; a use case's own
/// <c>unitOfWork.CommitAsync()</c> afterwards simply finds nothing left to save.
/// </para>
/// </remarks>
public sealed class ActivityRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<ActivityRepositoryAdapter> logger) : IActivityRepository
{
    private const string Origin = nameof(ActivityRepositoryAdapter);

    private DbSet<ActivityEntity> DbSet => context.Activities;

    public async Task<Result<Activity>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], cancellationToken).ConfigureAwait(false);
            return entity is null
                ? ActivityErrors.NotFound(id) with { Origin = Origin }
                : Result<Activity>.Success(ActivityRepositoryMapper.ToDomain(entity));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Activity with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await DbSet.AsNoTracking()
                .AnyAsync(entity => entity.Id == id, cancellationToken).ConfigureAwait(false);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of Activity with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<PagedResult<Activity>> GetAllAsync(
        PageQuery page, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PageAsync(DbSet.AsNoTracking(), page, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving all Activities");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> AddAsync(Activity aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = ActivityRepositoryMapper.ToEntity(aggregate);
            await DbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            aggregate.AssignId(entity.Id);

            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database update error adding Activity");
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error adding Activity");
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>
    /// Full-column overwrite. No update flow calls this yet — Tarea 8 covers only
    /// <c>GetActivities</c> and <c>CreateActivity</c> — so this exists solely to satisfy the
    /// repository contract's <c>Update</c> member. When an update use case is built,
    /// <see cref="ActivityRepositoryMapper.ToEntity"/>'s own warning applies: a blind copy would
    /// normalize legacy data DEC-6 forbids touching, so that future caller must copy changed
    /// columns selectively instead of relying on this method as written.
    /// </summary>
    public Result Update(Activity aggregate)
    {
        try
        {
            var entity = ActivityRepositoryMapper.ToEntity(aggregate);
            entity.Id = aggregate.Id;
            DbSet.Update(entity);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error updating Activity with id {Id}", aggregate.Id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], cancellationToken).ConfigureAwait(false);
            if (entity is null)
                return ActivityErrors.NotFound(id) with { Origin = Origin };

            DbSet.Remove(entity);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing Activity with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<PagedResult<Activity>> SearchAsync(
        ActivityFilter filter, PageQuery page, CancellationToken cancellationToken = default)
    {
        try
        {
            // opportunity is joined only to enforce its existence, mirroring the legacy API SP's
            // double INNER JOIN — it never appears in the projection or a Where.
            var query =
                from activity in DbSet.AsNoTracking()
                join deal in context.Set<Deal>().AsNoTracking() on activity.DealId equals deal.Id
                join opportunity in context.Set<Opportunity>().AsNoTracking()
                    on deal.OpportunityId equals opportunity.Id
                select new { activity, deal };

            if (filter.DealId.HasValue)
                query = query.Where(row => row.activity.DealId == filter.DealId.Value);

            if (filter.OpportunityId.HasValue)
                query = query.Where(row => row.activity.OpportunityId == filter.OpportunityId.Value);

            if (filter.DealStateId.HasValue)
                query = query.Where(row => row.deal.DealStateId == filter.DealStateId.Value);

            return await PageAsync(query.Select(row => row.activity), page, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error searching Activities");
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>
    /// Shared tail of <see cref="GetAllAsync"/> and <see cref="SearchAsync"/>: count and page in
    /// one round trip (the same shape <c>RepositoryBaseEF.GetAllAsync</c> uses), ordered by
    /// identity ascending, then map each row back to the aggregate.
    /// </summary>
    private static async Task<PagedResult<Activity>> PageAsync(
        IQueryable<ActivityEntity> query, PageQuery page, CancellationToken cancellationToken)
    {
        var result = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Items = g
                    .OrderBy(entity => entity.Id)
                    .Skip(page.Skip)
                    .Take(page.PageSize)
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (result is null)
            return PagedResult<Activity>.Success([], 0);

        IReadOnlyList<Activity> activities = [.. result.Items.Select(ActivityRepositoryMapper.ToDomain)];
        return PagedResult<Activity>.Success(activities, result.Total);
    }
}
