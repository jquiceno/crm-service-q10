using Activities.Domain.Aggregates;
using Activities.Domain.Errors;
using Activities.Domain.Queries;
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

/// <summary>EF Core persistence for the <see cref="ActivityAggregate"/>.</summary>
/// <remarks>
/// Can't inherit <see cref="RepositoryBaseEF{TAggregate, TId}"/>: it assumes the aggregate is what
/// EF maps, but here <see cref="Activity"/> is (F2.2), translated via
/// <see cref="ActivityRepositoryMapper"/>.
/// <para>
/// Two write members with deliberately different contracts: <see cref="CreateAsync"/> confirms its
/// own <c>INSERT</c>, because the caller needs the identity the database generates, and
/// <see cref="AddAsync"/> only queues, for a write that has to join a larger transaction. Neither
/// is a substitute for the other.
/// </para>
/// </remarks>
public sealed class ActivityRepository(
    ApplicationDbContext context,
    ILoggerPort<ActivityRepository> logger) : IActivityRepository
{
    private const string Origin = nameof(ActivityRepository);

    private DbSet<Activity> DbSet => context.Activities;

    public async Task<Result<ActivityAggregate>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], cancellationToken).ConfigureAwait(false);
            return entity is null
                ? ActivityErrors.NotFound(id) with { Origin = Origin }
                : Result<ActivityAggregate>.Success(ActivityRepositoryMapper.ToDomain(entity));
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

    public async Task<PagedResult<ActivityAggregate>> GetAllAsync(
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

    /// <summary>
    /// Inserts the activity and returns it carrying the identity the database generated.
    /// </summary>
    /// <remarks>
    /// Confirms its own <c>INSERT</c> instead of leaving it queued for the unit of work, which is
    /// the template's answer for exactly this case: the caller needs the <c>IDENTITY</c>, and that
    /// value does not exist until the row does. A caller that persists through here does not
    /// commit afterwards — there is nothing left to commit.
    /// <para>
    /// It also lets the insert classify its own constraint failures, which is what turns a foreign
    /// key against a deal that vanished mid-request into a conflict instead of a bare 500.
    /// </para>
    /// </remarks>
    public async Task<Result<ActivityAggregate>> CreateAsync(
        ActivityAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = ActivityRepositoryMapper.ToDocument(aggregate);
            await DbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // The IDENTITY is populated by SaveChanges; the aggregate is what the caller holds.
            aggregate.AssignId(entity.Id);
            return aggregate;
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database error inserting Activity");
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error inserting Activity");
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>
    /// Queues the insert for the unit of work, as <c>IRootRepository</c> defines it. It exists for
    /// a write that has to join a larger transaction; the creation flow uses
    /// <see cref="CreateAsync"/>, because it needs the generated identity back.
    /// </summary>
    /// <remarks>
    /// The aggregate that comes out of a commit here keeps <c>Id</c> at 0: the aggregate is not
    /// what EF tracks — <see cref="Activity"/> is — so nothing carries the identity back. A future
    /// caller that needs it must read it from <see cref="CreateAsync"/> instead.
    /// </remarks>
    public async Task<Result> AddAsync(ActivityAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = ActivityRepositoryMapper.ToDocument(aggregate);
            await DbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);

            return Result.Success();
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
    /// <remarks>
    /// It also expects an aggregate that already carries its identity. Calling it on one that was
    /// only staged by <see cref="AddAsync"/> — identity still 0 — makes EF read the default key as
    /// a second insert, not as the same row.
    /// </remarks>
    public Result Update(ActivityAggregate aggregate)
    {
        try
        {
            var entity = ActivityRepositoryMapper.ToDocument(aggregate);
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
            // returned); the advisor and the creator are left-joined, since either Person row may
            // not exist — migrated history has no advisor (§4.1), and a creator's row can vanish
            // just as well.
            var query =
                from activity in DbSet.AsNoTracking()
                join deal in context.Set<Deal>().AsNoTracking() on activity.DealId equals deal.Id
                join opportunity in context.Set<Opportunity>().AsNoTracking()
                    on deal.OpportunityId equals opportunity.Id
                join person in context.Set<Person>().AsNoTracking()
                    on activity.AdvisorId equals person.Code into advisors
                from advisor in advisors.DefaultIfEmpty()
                join creatorPerson in context.Set<Person>().AsNoTracking()
                    on activity.CreatedById equals creatorPerson.Code into creators
                from creator in creators.DefaultIfEmpty()
                select new { activity, deal, opportunity, advisor, creator };

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
                    row.advisor?.Identification,
                    row.creator?.FullName)),
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
    private static async Task<PagedResult<ActivityAggregate>> PageAsync(
        IQueryable<Activity> query, PageQuery page, CancellationToken cancellationToken)
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
            return PagedResult<ActivityAggregate>.Success([], 0);

        IReadOnlyList<ActivityAggregate> activities = [.. result.Items.Select(ActivityRepositoryMapper.ToDomain)];
        return PagedResult<ActivityAggregate>.Success(activities, result.Total);
    }
}
