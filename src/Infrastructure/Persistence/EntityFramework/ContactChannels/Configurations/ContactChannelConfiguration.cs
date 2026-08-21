using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels.Configurations;

public sealed class ContactChannelConfiguration : IEntityTypeConfiguration<ContactChannelEntity>
{
    public void Configure(EntityTypeBuilder<ContactChannelEntity> builder)
    {
        builder.ToTable("tbl_opo_medios_contacto");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("medcon_consecutivoP")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .HasColumnName("medcon_nombre")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(c => c.IsActive)
            .HasColumnName("medcon_estado");
    }
}
