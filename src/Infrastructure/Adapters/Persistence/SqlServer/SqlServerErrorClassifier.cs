using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Results.Errors;

namespace Infrastructure.Adapters.Persistence.SqlServer;

/// <summary>
/// Classifies SQL Server errors by error code. This approach is locale-independent,
/// unlike parsing error messages which vary based on server language settings.
/// Reference: https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
/// </summary>
internal static class SqlServerErrorClassifier
{
    internal static DomainError Classify(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
            return ClassifySqlException(sqlEx);

        return PersistenceErrors.Failure();
    }

    private static DomainError ClassifySqlException(SqlException ex) => ex.Number switch
    {
        // Primary key violation
        2627 => new ConflictError("A record with this value already exists."),

        // Unique index violation
        2601 => new ConflictError("A record with this value already exists."),

        // Foreign key constraint violation
        547 => new ConflictError("The operation conflicts with a related record."),

        // NOT NULL constraint violation
        515 => new PersistenceValidationError("A required field cannot be null."),

        // String or binary data would be truncated
        8152 => new PersistenceValidationError("A value exceeds the maximum allowed length."),

        // Deadlock
        1205 => new InternalError("The operation was aborted due to a deadlock. Please retry."),
        
        // Default: log the error but don't expose implementation details
        _ => PersistenceErrors.Failure()
    };
}
