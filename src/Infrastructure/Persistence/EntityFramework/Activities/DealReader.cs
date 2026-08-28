using Activities.Application.Ports;
using Activities.Domain.Models;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Reads a deal and the archived flag of its opportunity in a single round trip.
/// </summary>
/// <remarks>
/// Lives next to the context's persistence pieces, not in <c>Infrastructure/Adapters/</c>, and ends
/// in <c>Reader</c> — per <c>docs/plantilla/conceptos-reader-provider-repository.md</c>, which lists
/// the opposite as a common mistake.
/// <para>
/// The join is written explicitly and is a <b>left</b> join on purpose: an existing deal whose
/// opportunity row is missing must still report <c>Exists = true</c> instead of looking like a
/// missing deal.
/// </para>
/// </remarks>
public sealed class DealReader(ApplicationDbContext context) : IDealReader
{
    public async Task<DealContext> GetDealContextAsync(
        int dealId,
        CancellationToken cancellationToken = default)
    {
        var found = await (
            from deal in context.Set<Deal>().AsNoTracking()
            where deal.Id == dealId
            join opportunity in context.Set<Opportunity>().AsNoTracking()
                on deal.OpportunityId equals opportunity.Id into opportunities
            from opportunity in opportunities.DefaultIfEmpty()
            select new
            {
                deal.OpportunityId,
                Archived = opportunity == null ? null : opportunity.IsArchived,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // NULL means "not archived": the legacy reads this column as ISNULL(opo_estado, 0).
        return found is null
            ? DealContext.NotFound
            : new DealContext(Exists: true, found.OpportunityId, found.Archived ?? false);
    }
}
