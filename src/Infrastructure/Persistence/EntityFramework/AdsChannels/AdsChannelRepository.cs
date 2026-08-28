using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Queries;
using AdsChannel.Domain.Repositories;
using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.AdsChannels.Mappers;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.AdsChannels;

public sealed class AdsChannelRepository(
    ApplicationDbContext context,
    ILoggerPort<AdsChannelRepository> logger) : IAdsChannelRepository
{
    private const string Origin = nameof(AdsChannelRepository);

    private readonly DbSet<Entities.AdsChannel> _adsChannels = context.Set<Entities.AdsChannel>();

    public async Task<Result<AdsChannelAggregate>> GetByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _adsChannels
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return AdsChannelErrors.NotFound(id) with { Origin = Origin };

            return AdsChannelRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving AdsChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _adsChannels
                .AsNoTracking()
                .AnyAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of AdsChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Task<PagedResult<AdsChannelAggregate>> GetAllAsync(
        PageQuery page, CancellationToken cancellationToken = default) =>
        GetAsync(new AdsChannelFilter(null, null), page, cancellationToken);

    public async Task<Result<bool>> ExistsByNameAsync(
        string name, int? excludingId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _adsChannels
                .AsNoTracking()
                .AnyAsync(
                    x => x.Name == name && (excludingId == null || x.Id != excludingId),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of AdsChannel with name {Name}", name);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<PagedResult<AdsChannelAggregate>> GetAsync(
        AdsChannelFilter filter, PageQuery page, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _adsChannels.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.NameContains))
                query = query.Where(x => x.Name != null && x.Name.Contains(filter.NameContains));

            if (filter.IsActive is { } isActive)
                query = query.Where(x => x.IsActive == isActive);

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            var documents = await query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<AdsChannelAggregate> items =
                [.. documents.Select(AdsChannelRepositoryMapper.ToDomain)];

            return PagedResult<AdsChannelAggregate>.Success(items, totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error listing AdsChannel records");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> AddAsync(
        AdsChannelAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = AdsChannelRepositoryMapper.ToDocument(aggregate);
            await _adsChannels.AddAsync(document, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error adding AdsChannel");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Result Update(AdsChannelAggregate aggregate)
    {
        var document = AdsChannelRepositoryMapper.ToDocument(aggregate);
        context.Entry(document).State = EntityState.Modified;
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _adsChannels
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return AdsChannelErrors.NotFound(id) with { Origin = Origin };

            _adsChannels.Remove(document);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing AdsChannel with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<AdsChannelAggregate>> CreateAsync(
        AdsChannelAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = AdsChannelRepositoryMapper.ToDocument(aggregate);
            await _adsChannels.AddAsync(document, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // The Id is a SQL Server IDENTITY: only known now, after SaveChangesAsync populated it on
            // the tracked entity. Re-reconstruct from the entity rather than mutating the input aggregate.
            return AdsChannelRepositoryMapper.ToDomain(document);
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database error creating AdsChannel with name {Name}", aggregate.Name);

            if (SqlServerErrorClassifier.IsUniqueViolation(ex))
                return AdsChannelErrors.NameAlreadyExists(aggregate.Name) with { Origin = Origin };

            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error creating AdsChannel with name {Name}", aggregate.Name);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
