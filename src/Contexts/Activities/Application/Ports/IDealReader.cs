using Activities.Domain.Models;

namespace Activities.Application.Ports;

/// <summary>
/// Reads the deal and its opportunity from the foreign tables of the institution.
/// </summary>
/// <remarks>
/// A Reader, not a Repository: <c>tbl_opo_negocios</c> and <c>tbl_opo_oportunidades</c> are not
/// aggregates of this context, and repositories only work with aggregates. No <c>Port</c> suffix,
/// per <c>docs/plantilla/conceptos-reader-provider-repository.md</c>.
/// <para>
/// Not finding the deal is a valid outcome, not a failure, so the result is not wrapped in
/// <c>Result</c>. Turning "not found" into a domain error is the caller's job.
/// </para>
/// </remarks>
public interface IDealReader
{
    Task<DealContext> GetDealContextAsync(int dealId, CancellationToken cancellationToken = default);
}
