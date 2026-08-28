namespace Infrastructure.Persistence.EntityFramework.Activities.Entities;

/// <summary>
/// Read-only row of <c>tbl_per_personas</c>, mapped with only the columns the Activities context
/// consumes: resolving an identification number to a person code, and the advisor's display name.
/// </summary>
/// <remarks>
/// There is no repository for this table on purpose: repositories only work with aggregates, and a
/// person is a foreign read-only table. It is reached through a Reader.
/// <para>Column shapes verified in DB: <c>per_codigoP varchar(20) NOT NULL</c>,
/// <c>per_numero_identificacion varchar(20) NULL</c>,
/// <c>per_nombres_apellidos varchar(4000) NULL</c>.</para>
/// </remarks>
internal sealed class Person
{
    public string Code { get; set; } = string.Empty;

    public string? Identification { get; set; }

    public string? FullName { get; set; }
}
