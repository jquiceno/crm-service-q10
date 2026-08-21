using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Configurations;

/// <summary>
/// Maps the loss reason entity to the legacy <c>tbl_opo_causas</c> table.
/// </summary>
/// <remarks>
/// This is a Database First project: the column type and length are declared so EF generates the
/// right parameter type against the real schema (<c>varchar</c>, not <c>nvarchar</c>), never as
/// validation. The invariants live in the aggregate and in FluentValidation.
/// </remarks>
public sealed class LossReasonConfiguration : IEntityTypeConfiguration<Entities.LossReason>
{
    public void Configure(EntityTypeBuilder<Entities.LossReason> builder)
    {
        builder.ToTable("tbl_opo_causas");

        builder.HasKey(x => x.CauConsecutivoP);

        builder.Property(x => x.CauConsecutivoP)
            .HasColumnName("cau_consecutivoP")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CauNombre)
            .HasColumnName("cau_nombre")
            .HasColumnType("varchar(200)");

        builder.Property(x => x.CauEstado)
            .HasColumnName("cau_estado");
    }
}
