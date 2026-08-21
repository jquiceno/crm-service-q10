namespace Infrastructure.Persistence.EntityFramework.LossReasons.Entities;

/// <summary>
/// A row of the legacy <c>tbl_opo_causas</c> table.
/// </summary>
/// <remarks>
/// The nullability declared here is the <b>real</b> one of the database, not the desired one:
/// both <c>cau_nombre</c> and <c>cau_estado</c> accept NULL, and reading a NULL into a
/// non-nullable property makes SqlClient fail the whole query, not the row. The domain exposes
/// non-nullable values; the mapper normalizes.
/// </remarks>
public sealed class LossReason
{
    public int CauConsecutivoP { get; set; }

    public string? CauNombre { get; set; }

    public bool? CauEstado { get; set; }
}
