using Activities.Application.Ports;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Resolves an advisor's person code from an identification number.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when nothing matches: for a Reader that is a valid outcome, not a failure.
/// Deliberately does not look at roles — that validation belongs to the caller (DEC-17), so this
/// class never touches the security tables.
/// </remarks>
public sealed class AdvisorReader(ApplicationDbContext context) : IAdvisorReader
{
    public async Task<string?> ResolveByIdentificationAsync(
        string? identification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identification))
            return null;

        var trimmed = identification.Trim();

        return await context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.Identification == trimmed)
            .Select(person => person.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
