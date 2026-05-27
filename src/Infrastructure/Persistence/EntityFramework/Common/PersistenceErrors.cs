using Shared.Results.Errors;

namespace Infrastructure.Persistence.EntityFramework.Common;

internal static class PersistenceErrors
{
    internal static DomainError Failure() =>
        new("A persistence error occurred.", ErrorType.Internal);
}
