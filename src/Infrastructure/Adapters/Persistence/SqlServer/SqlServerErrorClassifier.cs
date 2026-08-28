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
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;
    // 547 covers FOREIGN KEY, REFERENCE and CHECK alike; the number does not say which one failed.
    private const int ConstraintConflict = 547;
    private const int NotNullViolation = 515;
    private const int DataTruncation = 8152;
    private const int Deadlock = 1205;

    internal static DomainError Classify(DbUpdateException ex, string origin)
    {
        if (ex.InnerException is SqlException sqlEx)
            return ClassifySqlException(sqlEx) with { Origin = origin };

        return PersistenceErrors.Failure(origin);
    }

    internal static DomainError Classify(SqlException ex, string origin) =>
        ClassifySqlException(ex) with { Origin = origin };

    private static DomainError ClassifySqlException(SqlException ex) => ex.Number switch
    {
        PrimaryKeyViolation or UniqueIndexViolation =>
            new ConflictError("A record with this value already exists."),

        ConstraintConflict => new ConflictError("The operation conflicts with a related record."),

        NotNullViolation => new PersistenceValidationError("A required field cannot be null."),

        DataTruncation => new PersistenceValidationError("A value exceeds the maximum allowed length."),

        Deadlock => new InternalError("The operation was aborted due to a deadlock. Please retry."),

        // Default: log the error but don't expose implementation details
        _ => PersistenceErrors.Failure(string.Empty)
    };
}
