using Activities.Domain.Aggregates;
using Activities.Domain.Errors;
using Activities.Domain.Filters;
using Activities.Domain.Models;
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

/// <summary>EF Core persistence for the <see cref="Activity"/> aggregate.</summary>
/// <remarks>
/// Can't inherit <see cref="RepositoryBaseEF{TAggregate, TId}"/>: it assumes the aggregate is what
/// EF maps, but here <see cref="ActivityEntity"/> is (F2.2), translated via
/// <see cref="ActivityRepositoryMapper"/>. <see cref="AddAsync"/> saves immediately, unlike the
/// rest — the id is a SQL <c>IDENTITY</c> unknown until insert, with no later point to copy it back.
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
    /// Full-column overwrite; no update flow exists yet. A real one must copy changed columns
    /// selectively instead (DEC-6), not reuse this as-is.
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

    public async Task<PagedResult<ActivityListItem>> SearchAsync(
        ActivityFilter filter, PageQuery page, CancellationToken cancellationToken = default)
    {
        try
        {
            // Deal and opportunity are inner-joined (legacy SP parity: a row without them is not
            // returned); the advisor is left-joined, since migrated history has none (§4.1).
            var query =
                from activity in DbSet.AsNoTracking()
                join deal in context.Set<Deal>().AsNoTracking() on activity.DealId equals deal.Id
                join opportunity in context.Set<Opportunity>().AsNoTracking()
                    on deal.OpportunityId equals opportunity.Id
                join person in context.Set<Person>().AsNoTracking()
                    on activity.AdvisorId equals person.Code into advisors
                from advisor in advisors.DefaultIfEmpty()
                select new { activity, deal, opportunity, advisor };

            if (filter.DealId.HasValue)
                query = query.Where(row => row.activity.DealId == filter.DealId.Value);

            if (filter.OpportunityId.HasValue)
                query = query.Where(row => row.activity.OpportunityId == filter.OpportunityId.Value);

            if (filter.DealStateId.HasValue)
                query = query.Where(row => row.deal.DealStateId == filter.DealStateId.Value);

            // Same count+page shape as PageAsync, over the joined row instead of the bare entity:
            // the names must come from the very rows this page selects, not a second query.
            var result = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Items = g
                        .OrderBy(row => row.activity.Id)
                        .Skip(page.Skip)
                        .Take(page.PageSize)
                        .ToList(),
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (result is null)
                return PagedResult<ActivityListItem>.Success([], 0);

            IReadOnlyList<ActivityListItem> items =
            [
                .. result.Items.Select(row => new ActivityListItem(
                    ActivityRepositoryMapper.ToDomain(row.activity),
                    row.deal.Name,
                    row.opportunity.Name,
                    row.advisor?.FullName,
                    row.advisor?.Identification)),
            ];

            return PagedResult<ActivityListItem>.Success(items, result.Total);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error searching Activities");
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>Count+page tail of <see cref="GetAllAsync"/>.</summary>
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
