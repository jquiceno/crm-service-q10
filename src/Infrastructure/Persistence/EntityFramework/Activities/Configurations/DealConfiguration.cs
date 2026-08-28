using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.Activities.Configurations;

/// <summary>
/// Minimal read-only mapping of <c>tbl_opo_negocios</c>.
/// </summary>
/// <remarks>
/// Explicit column mapping, only the four columns the context reads — the less surface mapped, the
/// less exposure to the schema drift measured across institutions.
/// <para>
/// No navigation properties and no relationships are configured: references travel by id and the
/// readers write their joins explicitly (DEC-16). That also keeps EF from inventing cascade
/// semantics the legacy foreign keys do not have — they are all <c>NO_ACTION</c>.
/// </para>
/// </remarks>
internal sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("tbl_opo_negocios");

        builder.HasKey(deal => deal.Id);

        // The real column is IDENTITY, but this service never inserts here; declaring the key as
        // never-generated keeps the read-only intent explicit.
        builder.Property(deal => deal.Id)
            .HasColumnName("neg_consecutivoP")
            .ValueGeneratedNever();

        builder.Property(deal => deal.OpportunityId)
            .HasColumnName("neg_opo_consecutivo")
            .IsRequired();

        builder.Property(deal => deal.DealStateId)
            .HasColumnName("neg_negest_consecutivo")
            .IsRequired();

        builder.Property(deal => deal.Name)
            .HasColumnName("neg_nombre")
            .HasMaxLength(1000);
    }
}
