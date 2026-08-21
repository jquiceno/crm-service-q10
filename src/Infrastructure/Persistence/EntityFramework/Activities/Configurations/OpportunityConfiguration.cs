using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.Activities.Configurations;

/// <summary>
/// Minimal read-only mapping of <c>tbl_opo_oportunidades</c>.
/// </summary>
internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("tbl_opo_oportunidades");

        builder.HasKey(opportunity => opportunity.Id);

        builder.Property(opportunity => opportunity.Id)
            .HasColumnName("opo_consecutivoP")
            .ValueGeneratedNever();

        builder.Property(opportunity => opportunity.Name)
            .HasColumnName("opo_nombre")
            .HasMaxLength(1000);

        // Nullable on purpose: the column is bit NULL and the legacy reads it as ISNULL(opo_estado, 0).
        builder.Property(opportunity => opportunity.IsArchived)
            .HasColumnName("opo_estado");
    }
}
