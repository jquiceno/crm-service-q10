using Activities.Domain.Aggregates;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Drift-safe mapping of the legacy <c>tbl_opo_negocios_actividades</c> (F2.2) over the
/// persistence entity — the aggregate never touches EF; the repository mapper translates in
/// both directions.
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
internal sealed class ActivityConfiguration : IEntityTypeConfiguration<ActivityEntity>
{
    public void Configure(EntityTypeBuilder<ActivityEntity> builder)
    {
        builder.ToTable("tbl_opo_negocios_actividades");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("negact_consecutivoP")
            .ValueGeneratedOnAdd();

        // Nullable in the legacy DB but 0 NULLs across 605 real databases; NOT NULL in the
        // domain (DEC-1) — a NULL here is invalid data and must fail loudly, not default to 0.
        builder.Property(e => e.DealId)
            .HasColumnName("negact_neg_consecutivo");

        builder.Property(e => e.OpportunityId)
            .HasColumnName("negact_opo_consecutivo");

        builder.Property(e => e.Type)
            .HasColumnName("negact_tipo")
            .HasColumnType("char(1)");

        // The UI calls this column "Descripción" — the inverted semantics are deliberate (§4).
        builder.Property(e => e.Title)
            .HasColumnName("negact_titulo")
            .HasColumnType("varchar(500)");

        // The UI calls this column "Resultado". The logical contract is MAX (DEC-3); the tenants
        // still on varchar(2000) are protected by the phase-1 API edge cap, not by this mapping.
        builder.Property(e => e.OutcomeText)
            .HasColumnName("negact_descripcion")
            .HasColumnType("varchar(max)");

        builder.Property(e => e.OutcomeCode)
            .HasColumnName("negact_resultado")
            .HasColumnType("char(1)");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("negact_fecha")
            .HasColumnType("datetime");

        builder.Property(e => e.DueAt)
            .HasColumnName("negact_fecha_vencimiento")
            .HasColumnType("datetime");

        builder.Property(e => e.IsCompleted)
            .HasColumnName("negact_completada");

        builder.Property(e => e.IsCancelled)
            .HasColumnName("negact_anulada");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("negact_fecha_resuelto")
            .HasColumnType("datetime");

        // Nullable: migrated history exists without an advisor (§4.1).
        builder.Property(e => e.AdvisorId)
            .HasColumnName("negact_asesor")
            .HasColumnType("varchar(20)");

        builder.Property(e => e.CreatedById)
            .HasColumnName("negact_per_codigo")
            .HasColumnType("varchar(20)");
    }
}
