using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Result = Shared.Results.Result;

namespace Infrastructure.Adapters.Persistence;

public sealed class UnitOfWorkAdapter(
    ApplicationDbContext context,
    ILoggerPort<UnitOfWorkAdapter> logger) : IUnitOfWorkPort
{
    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database update error during commit");
            return SqlServerErrorClassifier.Classify(ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Unexpected error during commit");
            return PersistenceErrors.Failure();
        }
    }
}
