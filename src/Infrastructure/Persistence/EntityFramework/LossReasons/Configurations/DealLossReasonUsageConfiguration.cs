using Infrastructure.Persistence.EntityFramework.LossReasons.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Configurations;

internal sealed class DealLossReasonUsageConfiguration : IEntityTypeConfiguration<DealLossReasonUsage>
{
    public void Configure(EntityTypeBuilder<DealLossReasonUsage> builder)
    {
        builder.ToTable("tbl_opo_negocios");

        // Keyless entity: no primary key, no insert/update/delete operations.
        builder.HasNoKey();

        builder.Property(x => x.NegCauConsecutivo)
            .HasColumnName("neg_cau_consecutivo");
    }
}
