using Shared.Results.Errors;

namespace Infrastructure.Persistence.EntityFramework.Common;

internal static class PersistenceErrors
{
    internal static InternalError Failure(string origin) =>
        new("A persistence error occurred.") { Origin = origin };
}
