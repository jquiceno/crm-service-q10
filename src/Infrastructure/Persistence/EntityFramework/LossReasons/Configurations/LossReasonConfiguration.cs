using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Configurations;

public sealed class LossReasonConfiguration : IEntityTypeConfiguration<Entities.LossReason>
{
    public void Configure(EntityTypeBuilder<Entities.LossReason> builder)
    {
        builder.ToTable("tbl_opo_causas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("cau_consecutivoP")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasColumnName("cau_nombre")
            .HasColumnType("varchar(200)");

        builder.Property(x => x.IsActive)
            .HasColumnName("cau_estado");
    }
}
