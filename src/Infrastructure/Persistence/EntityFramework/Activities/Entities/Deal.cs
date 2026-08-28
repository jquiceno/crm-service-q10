namespace Infrastructure.Persistence.EntityFramework.Activities.Entities;

/// <summary>
/// Read-only row of <c>tbl_opo_negocios</c>, mapped with only the columns the Activities context
/// consumes.
/// </summary>
/// <remarks>
/// A persistence entity, never an aggregate: the deal belongs to another context and this service
/// only reads it. Nullability mirrors the real database, not the desired one — reading a
/// <c>NULL</c> into a non-nullable property makes SqlClient throw for the whole query.
/// <para>Column shapes verified in DB: <c>neg_opo_consecutivo</c> and
/// <c>neg_negest_consecutivo</c> are <c>int NOT NULL</c>; <c>neg_nombre</c> is
/// <c>varchar(1000) NULL</c>.</para>
/// </remarks>
internal sealed class Deal
{
    public int Id { get; set; }

    public int OpportunityId { get; set; }

    public int DealStateId { get; set; }

    public string? Name { get; set; }
}
