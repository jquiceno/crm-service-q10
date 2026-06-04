using Infraestructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infraestructure.MasterAccess.Persistence.EntityFramework.Tenants.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tblc_entidades");

        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code)
            .HasColumnName("ent_codigoP")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Database)
            .HasColumnName("ent_bd")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.ServerDatabase)
            .HasColumnName("ent_serbd_consecutivo")
            .IsRequired();
    }
}
