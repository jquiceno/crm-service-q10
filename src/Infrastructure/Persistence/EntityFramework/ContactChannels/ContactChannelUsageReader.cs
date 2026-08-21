using ContactChannel.Application.Ports;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels;

public sealed class ContactChannelUsageReader(
    ApplicationDbContext context,
    ILoggerPort<ContactChannelUsageReader> logger) : IContactChannelUsageReader
{
    private const string Origin = nameof(ContactChannelUsageReader);

    // Scalar query instead of a mapped entity: tbl_opo_oportunidades belongs to another aggregate
    // and this context must not model it or navigate to it.
    public async Task<Result<bool>> IsReferencedAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var references = await context.Database
                .SqlQuery<int>(
                    $"SELECT COUNT(1) AS Value FROM tbl_opo_oportunidades WHERE opo_medcon_consecutivo = {id}")
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(references > 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking whether ContactChannel {Id} is referenced", id);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
