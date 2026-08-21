using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EntityFramework.Activities.Configurations;

/// <summary>
/// Minimal read-only mapping of <c>tbl_per_personas</c>.
/// </summary>
/// <remarks>
/// Only three columns are mapped. The role tables are deliberately absent: validating the
/// advisor's role is the caller's responsibility (DEC-17), so this context never reaches
/// <c>tbl_seg_roles_personas</c>.
/// </remarks>
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("tbl_per_personas");

        builder.HasKey(person => person.Code);

        builder.Property(person => person.Code)
            .HasColumnName("per_codigoP")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(person => person.Identification)
            .HasColumnName("per_numero_identificacion")
            .HasMaxLength(20);

        builder.Property(person => person.FullName)
            .HasColumnName("per_nombres_apellidos")
            .HasMaxLength(4000);
    }
}
