using LossReason.Domain.Aggregates;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Mappers;

/// <summary>
/// Translates between the legacy <c>tbl_opo_causas</c> row and the aggregate.
/// </summary>
public static class LossReasonRepositoryMapper
{
    /// <summary>
    /// Rebuilds the aggregate from a persisted row. Uses <c>Reconstruct</c>, never <c>Create</c>:
    /// stored data is not re-validated, which is what lets a legacy name longer than
    /// <see cref="LossReasonAggregate.NameMaxLength"/> be read without error.
    /// The NULLs the real schema allows are normalized here.
    /// </summary>
    public static LossReasonAggregate ToDomain(Entities.LossReason document) =>
        LossReasonAggregate.Reconstruct(
            document.CauConsecutivoP,
            document.CauNombre ?? string.Empty,
            document.CauEstado ?? false);

    /// <summary>
    /// Projects the aggregate onto a row. <c>cau_consecutivoP</c> is left untouched: it is an
    /// IDENTITY column and the database assigns it on insert. <c>CreatedAt</c>/<c>UpdatedAt</c>
    /// are not persisted either — the legacy table has no columns for them.
    /// </summary>
    public static Entities.LossReason ToDocument(LossReasonAggregate aggregate) =>
        new()
        {
            CauNombre = aggregate.Name,
            CauEstado = aggregate.IsActive
        };
}
