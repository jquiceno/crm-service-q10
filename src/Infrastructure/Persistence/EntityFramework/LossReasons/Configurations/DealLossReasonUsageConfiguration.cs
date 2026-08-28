using Infrastructure.Persistence.EntityFramework.LossReasons.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Configurations;

internal sealed class DealLossReasonUsageConfiguration : IEntityTypeConfiguration<DealLossReasonUsage>
{
    public void Configure(EntityTypeBuilder<DealLossReasonUsage> builder)
    {
        builder.ToTable("tbl_opo_negocios");

        // Read-only projection: the entity maps a single column, so it exposes
        // no candidate key and EF Core requires HasNoKey. Removing it makes the
        // model invalid and IsUsedAsync degrades to PersistenceErrors.Failure
        // instead of answering the usage check (verified by removing this line
        // and watching the reader tests fail).
        //
        // It is also the constraint that matters most here: it is what makes
        // the projection read-only, since a keyless type cannot be tracked,
        // inserted, updated or deleted. Writing neg_cau_consecutivo belongs to
        // the Negocio aggregate in the monolith, which is out of scope (GAP-7).
        builder.HasNoKey();

        // Nullable int: the column is optional in the schema, and a deal with
        // no loss reason simply has no assignment yet.
        builder.Property(x => x.LossReasonId)
            .HasColumnName("neg_cau_consecutivo")
            .HasColumnType("int")
            .IsRequired(false);

        // The schema declares FK_tbl_opo_causas_tbl_opo_negocios on this column
        // (referencing tbl_opo_causas.cau_consecutivoP, ON DELETE NO_ACTION).
        // It is deliberately NOT mapped as a relationship: this is a Database
        // First mapping with no migrations, so declaring it would change no
        // schema, and modelling it would couple this read-only projection to
        // the LossReason entity that D7 keeps it independent from. The
        // constraint is enforced by SQL Server, and it is precisely what makes
        // deleting a used reason fail with error 547 — the case IsUsedAsync
        // exists to detect before that happens.
    }
}
