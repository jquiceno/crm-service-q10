using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels.Configurations;

// Keyless on purpose: this context reads one column of a table it does not own, so it declares no
// key, no navigation and no other column. The opportunity aggregate is not modelled here.
public sealed class ContactChannelUsageConfiguration : IEntityTypeConfiguration<ContactChannelUsage>
{
    public void Configure(EntityTypeBuilder<ContactChannelUsage> builder)
    {
        builder.HasNoKey();

        builder.ToTable("tbl_opo_oportunidades");

        builder.Property(u => u.ContactChannelId)
            .HasColumnName("opo_medcon_consecutivo");
    }
}
