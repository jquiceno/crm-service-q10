using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Errors;

namespace Infrastructure.Persistence.EntityFramework.Common;

internal static class DbErrorClassifier
{
    public static DomainError Classify(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
            return ClassifySqlException(sqlEx);

        return SharedErrors.PersistenceFailure(ex.InnerException?.Message ?? ex.Message);
    }

    // https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
    private static DomainError ClassifySqlException(SqlException ex) => ex.Number switch
    {
        2627 => SharedErrors.UniqueConstraintViolation(NthSingleQuoted(ex.Message, 0)), // PK: first quoted = constraint name
        2601 => SharedErrors.UniqueConstraintViolation(NthSingleQuoted(ex.Message, 1)), // Unique index: second quoted = index name
        547  => SharedErrors.ForeignKeyViolation(FirstDoubleQuoted(ex.Message)),        // FK: first double-quoted = constraint name
        515  => SharedErrors.NullConstraintViolation(NthSingleQuoted(ex.Message, 0)),  // NULL: first quoted = column name
        8152 => SharedErrors.DataTruncation(NthSingleQuoted(ex.Message, 1)),           // Truncation: second quoted = column name
        1205 => SharedErrors.Deadlock(),
        _    => SharedErrors.PersistenceFailure(ex.Message)
    };

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
