using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Errors;

namespace Infrastructure.Adapters.Persistence.SqlServer;

// https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
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
        2627 => new DomainError(ForDuplicate("primary key", NthSingleQuoted(ex.Message, 0)), ErrorType.Conflict),
        2601 => new DomainError(ForDuplicate("unique index", NthSingleQuoted(ex.Message, 1)), ErrorType.Conflict),
        547  => new DomainError(ForForeignKey(FirstDoubleQuoted(ex.Message)), ErrorType.Conflict),
        515  => new DomainError(ForNullField(NthSingleQuoted(ex.Message, 0)), ErrorType.Validation),
        8152 => new DomainError(ForTruncation(NthSingleQuoted(ex.Message, 1)), ErrorType.Validation),
        1205 => new DomainError("The operation was aborted due to a deadlock. Please retry.", ErrorType.Internal),
        _    => PersistenceErrors.Failure()
    };

    private static string ForDuplicate(string type, string? name) =>
        name is not null ? $"Duplicate {type} '{name}'." : "A duplicate record already exists.";

    private static string ForForeignKey(string? name) =>
        name is not null ? $"Foreign key violation: '{name}'." : "The operation conflicts with a related record.";

    private static string ForNullField(string? column) =>
        column is not null ? $"Required field '{column}' cannot be null." : "A required field cannot be null.";

    private static string ForTruncation(string? column) =>
        column is not null ? $"Value for '{column}' exceeds the maximum allowed length." : "A value exceeds the maximum allowed length.";

    private static string? NthSingleQuoted(string message, int n)
    {
        var count = 0;
        var pos = 0;
        while (true)
        {
            var start = message.IndexOf('\'', pos);
            if (start < 0) return null;
            var end = message.IndexOf('\'', start + 1);
            if (end < 0) return null;
            if (count == n) return message[(start + 1)..end];
            count++;
            pos = end + 1;
        }
    }

    private static string? FirstDoubleQuoted(string message)
    {
        var start = message.IndexOf('"');
        if (start < 0) return null;
        var end = message.IndexOf('"', start + 1);
        if (end < 0) return null;
        return message[(start + 1)..end];
    }
}
