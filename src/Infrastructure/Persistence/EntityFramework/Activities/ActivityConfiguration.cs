using Activities.Domain.Aggregates;
using Activities.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Drift-safe mapping of the legacy <c>tbl_opo_negocios_actividades</c> (F2.2).
/// </summary>
/// <remarks>
/// Only the columns present in all 378 institutions are mapped (DEC-3): the per-tenant
/// <c>ConsecutivoActMiG</c> and the out-of-scope <c>negact_descripcion_virtual</c> are never
/// referenced, so their presence, absence or physical position cannot break reads or writes.
/// No navigation properties nor relations are configured — references are plain IDs and joins
/// are written explicitly where needed (DEC-16), which also keeps EF from assuming cascade
/// semantics the legacy <c>NO_ACTION</c> FKs do not have. The legacy chars live only in
/// <see cref="LegacyActivityCodes"/> (DEC-15). No EF migrations ever run against the legacy
/// databases — this is mapping, not schema.
/// </remarks>
public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    /// <summary>
    /// Shadow property holding the raw <c>negact_resultado</c> char. It cannot be a value
    /// converter on <see cref="Activity.OutcomeType"/> because the char's meaning depends on
    /// <c>negact_tipo</c> and a converter sees a single column;
    /// <see cref="ActivityLegacyCodeInterceptor"/> fills it on save and resolves it on read.
    /// </summary>
    internal const string OutcomeTypeCodeProperty = "OutcomeTypeCode";

    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("tbl_opo_negocios_actividades");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("negact_consecutivoP")
            .ValueGeneratedOnAdd();

        // Nullable in the legacy DB but 0 NULLs across 605 real databases; NOT NULL in the
        // domain (DEC-1) — a NULL here is invalid data and must fail loudly, not default to 0.
        builder.Property(a => a.DealId)
            .HasColumnName("negact_neg_consecutivo");

        builder.Property(a => a.OpportunityId)
            .HasColumnName("negact_opo_consecutivo");

        builder.Property(a => a.Type)
            .HasColumnName("negact_tipo")
            .HasColumnType("char(1)")
            .HasConversion(
                type => LegacyActivityCodes.ToTypeCode(type),
                code => LegacyActivityCodes.ToType(code));

        // The UI calls this column "Descripción" — the inverted semantics are deliberate (§4).
        builder.Property(a => a.Description)
            .HasColumnName("negact_titulo")
            .HasColumnType("varchar(500)")
            .HasConversion(
                description => description!.Value,
                value => Description.Reconstruct(value));

        // The UI calls this column "Resultado". The logical contract is MAX (DEC-3); the tenants
        // still on varchar(2000) are protected by the phase-1 API edge cap, not by this mapping.
        builder.Property(a => a.Outcome)
            .HasColumnName("negact_descripcion")
            .HasColumnType("varchar(max)")
            .HasConversion(
                outcome => outcome!.Value,
                value => Outcome.Reconstruct(value));

        builder.Ignore(a => a.OutcomeType);
        builder.Property<string>(OutcomeTypeCodeProperty)
            .HasColumnName("negact_resultado")
            .HasColumnType("char(1)")
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("negact_fecha")
            .HasColumnType("datetime")
            .IsRequired();

        builder.Property(a => a.DueAt)
            .HasColumnName("negact_fecha_vencimiento")
            .HasColumnType("datetime");

        builder.Property(a => a.CompletedAt)
            .HasColumnName("negact_fecha_resuelto")
            .HasColumnType("datetime");

        // The legacy bit pair collapses into Activity.Status (NULL ⇒ Scheduled — DEC-6); the
        // domain keeps the bits as nullable fields so historic NULL rows round-trip untouched.
        // Because Status is computed (ignored), SQL cannot filter/order by it: queries must use
        // EF.Property<bool?>(a, "_isCompleted") / "_isCancelled" — the list query (F2.4) will.
        builder.Ignore(a => a.Status);
        builder.Property<bool?>("_isCompleted").HasColumnName("negact_completada");
        builder.Property<bool?>("_isCancelled").HasColumnName("negact_anulada");

        // Optional in legacy data: migrated history exists without an advisor (§4.1). The
        // creation invariant still requires it — optionality here is read-side drift tolerance.
        builder.Property(a => a.AdvisorId)
            .HasColumnName("negact_asesor")
            .HasColumnType("varchar(20)")
            .HasConversion(
                advisor => advisor!.Value,
                value => PersonCode.Reconstruct(value))
            .IsRequired(false);

        builder.Property(a => a.CreatedById)
            .HasColumnName("negact_per_codigo")
            .HasColumnType("varchar(20)")
            .HasConversion(
                creator => creator.Value,
                value => PersonCode.Reconstruct(value));

        // The legacy table has no updated column (and no domain flow updates an activity yet).
        builder.Ignore(a => a.UpdatedAt);
    }
}
