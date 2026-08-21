using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses.Configurations;

/// <summary>
/// <c>HasMaxLength</c> and <c>IsUnicode</c> are here so EF emits <c>varchar</c> parameters that fit
/// the real schema, not as validation — that lives in the domain. Nothing is <c>IsRequired()</c>:
/// the table admits NULL in the four non-key columns.
/// </summary>
internal sealed class BusinessStatusConfiguration : IEntityTypeConfiguration<Entities.BusinessStatus>
{
    public void Configure(EntityTypeBuilder<Entities.BusinessStatus> builder)
    {
        builder.ToTable("tbl_opo_negocios_estados");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("negest_consecutivoP")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasColumnName("negest_nombre")
            .HasMaxLength(200)
            .IsUnicode(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("negest_estado");

        builder.Property(x => x.Percentage)
            .HasColumnName("negest_porcentaje")
            .HasPrecision(20, 5);

        builder.Property(x => x.Color)
            .HasColumnName("negest_color")
            .HasMaxLength(20)
            .IsUnicode(false);

        // No navigations towards the three incoming foreign keys: the aggregate does not need to
        // see its referencing tables, and their only effect here is the 547 raised on delete.
    }
}
