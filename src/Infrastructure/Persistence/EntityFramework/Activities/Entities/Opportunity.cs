namespace Infrastructure.Persistence.EntityFramework.Activities.Entities;

/// <summary>
/// Read-only row of <c>tbl_opo_oportunidades</c>, mapped with only the columns the Activities
/// context consumes.
/// </summary>
/// <remarks>
/// <see cref="IsArchived"/> is nullable because <c>opo_estado</c> is <c>bit NULL</c> [verified in
/// DB] — which is why every legacy stored procedure reads it as <c>ISNULL(opo_estado, 0)</c>. The
/// reader applies the same rule: <c>NULL</c> means not archived.
/// </remarks>
internal sealed class Opportunity
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsArchived { get; set; }
}
