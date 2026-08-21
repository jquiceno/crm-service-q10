using AdsChannel.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.AdsChannels.Configurations;

public sealed class AdsChannelConfiguration : IEntityTypeConfiguration<Entities.AdsChannel>
{
    public void Configure(EntityTypeBuilder<Entities.AdsChannel> builder)
    {
        builder.ToTable("tbl_opo_medios_publicitarios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("medpub_consecutivoP").ValueGeneratedOnAdd();
        builder.Property(x => x.Name)
            .HasColumnName("medpub_nombre")
            .HasMaxLength(AdsChannelAggregate.MaxNameLength)
            .IsUnicode(false);
        builder.Property(x => x.IsActive).HasColumnName("medpub_estado");
    }
}
