using ContactChannel.Application.Ports;
using Infrastructure.Persistence.EntityFramework.Common;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels;

public sealed class ContactChannelUsageReader(
    ApplicationDbContext context,
    ILoggerPort<ContactChannelUsageReader> logger) : IContactChannelUsageReader
{
    private const string Origin = nameof(ContactChannelUsageReader);

    private readonly DbSet<ContactChannelUsage> _usages = context.Set<ContactChannelUsage>();

    public async Task<Result<bool>> IsReferencedAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var isReferenced = await _usages
                .AnyAsync(usage => usage.ContactChannelId == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(isReferenced);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking whether ContactChannel {Id} is referenced", id);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
