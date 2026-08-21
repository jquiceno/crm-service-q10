using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Queries;
using ContactChannel.Domain.Repositories;
using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.Common;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Mappers;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels;

public sealed class ContactChannelRepository(
    ApplicationDbContext context,
    ILoggerPort<ContactChannelRepository> logger) : IContactChannelRepository
{
    private const string Origin = nameof(ContactChannelRepository);

    private static readonly ContactChannelFilter Unfiltered = new(IsActive: null, SearchName: null);

    private readonly DbSet<ContactChannelEntity> _contactChannels = context.ContactChannels;

    public async Task<Result<ContactChannelAggregate>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _contactChannels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return ContactChannelErrors.NotFound(id) with { Origin = Origin };

            return ContactChannelRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving ContactChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _contactChannels
                .AsNoTracking()
                .AnyAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of ContactChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Task<PagedResult<ContactChannelAggregate>> GetAllAsync(
        PageQuery page,
        CancellationToken cancellationToken = default) =>
        GetAsync(Unfiltered, page, cancellationToken);

    public async Task<PagedResult<ContactChannelAggregate>> GetAsync(
        ContactChannelFilter filter,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _contactChannels.AsNoTracking();

            if (filter.IsActive is bool isActive)
                query = query.Where(c => (c.IsActive ?? false) == isActive);

            if (!string.IsNullOrWhiteSpace(filter.SearchName))
            {
                var searchName = filter.SearchName.Trim();
                query = query.Where(c => c.Name != null && c.Name.Contains(searchName));
            }

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            var documents = await query
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Id)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<ContactChannelAggregate> aggregates =
                [.. documents.Select(ContactChannelRepositoryMapper.ToDomain)];

            return PagedResult<ContactChannelAggregate>.Success(aggregates, totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving ContactChannel page with filter {Filter}", filter);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> AddAsync(
        ContactChannelAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = ContactChannelRepositoryMapper.ToNewDocument(aggregate);

            await _contactChannels.AddAsync(document, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error adding ContactChannel");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<ContactChannelAggregate>> CreateAsync(
        ContactChannelAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = ContactChannelRepositoryMapper.ToNewDocument(aggregate);

            await _contactChannels.AddAsync(document, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ContactChannelRepositoryMapper.ToDomain(document);
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database error inserting ContactChannel");
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error inserting ContactChannel");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Result Update(ContactChannelAggregate aggregate)
    {
        try
        {
            if (aggregate.Id == default)
            {
                logger.Warning("Update called with an unassigned ContactChannel identifier.");
                return ContactChannelErrors.NotFound(aggregate.Id) with { Origin = Origin };
            }

            var tracked = _contactChannels.Local.FirstOrDefault(c => c.Id == aggregate.Id);

            if (tracked is null)
            {
                tracked = ContactChannelRepositoryMapper.ToDocument(aggregate);
            }
            else
            {
                ContactChannelRepositoryMapper.CopyTo(aggregate, tracked);
            }

            context.Entry(tracked).State = EntityState.Modified;

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error updating ContactChannel with id {Id}", aggregate.Id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _contactChannels
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return ContactChannelErrors.NotFound(id) with { Origin = Origin };

            _contactChannels.Remove(document);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing ContactChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
