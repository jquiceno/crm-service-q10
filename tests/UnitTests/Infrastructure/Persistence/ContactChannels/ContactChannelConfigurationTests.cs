using ContactChannel.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.ContactChannels;

public sealed class ContactChannelConfigurationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ContactChannelModel;Trusted_Connection=True;")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IEntityType GetContactChannelEntityType()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(ContactChannelEntity));
        entityType.ShouldNotBeNull();

        return entityType;
    }

    [Fact]
    public void Configure_MapsTheEntityToTheLegacyTable()
    {
        GetContactChannelEntityType().GetTableName().ShouldBe("tbl_opo_medios_contacto");
    }

    [Fact]
    public void Configure_MapsTheIdentifierToTheLegacyIdentityColumn()
    {
        var entityType = GetContactChannelEntityType();

        var id = entityType.FindProperty(nameof(ContactChannelEntity.Id));
        id.ShouldNotBeNull();
        id.GetColumnName().ShouldBe("medcon_consecutivoP");
        id.ValueGenerated.ShouldBe(ValueGenerated.OnAdd);
        id.IsNullable.ShouldBeFalse();

        var primaryKey = entityType.FindPrimaryKey();
        primaryKey.ShouldNotBeNull();
        primaryKey.Properties.Select(p => p.Name).ShouldBe([nameof(ContactChannelEntity.Id)]);
    }

    [Fact]
    public void Configure_MapsTheNameAsNonUnicodeVarchar()
    {
        var name = GetContactChannelEntityType().FindProperty(nameof(ContactChannelEntity.Name));

        name.ShouldNotBeNull();
        name.GetColumnName().ShouldBe("medcon_nombre");
        name.GetColumnType().ShouldBe("varchar(100)");
        name.GetMaxLength().ShouldBe(100);
        name.IsUnicode().ShouldBe(false);
        name.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Configure_MapsTheStateToTheLegacyBitColumn()
    {
        var isActive = GetContactChannelEntityType().FindProperty(nameof(ContactChannelEntity.IsActive));

        isActive.ShouldNotBeNull();
        isActive.GetColumnName().ShouldBe("medcon_estado");
        isActive.GetColumnType().ShouldBe("bit");
        isActive.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Configure_AgreesWithTheLengthTheAggregateEnforces()
    {
        ContactChannelAggregate.NameMaxLength.ShouldBe(100);
    }

    [Fact]
    public void Configure_DoesNotMapTheAggregate()
    {
        using var context = CreateContext();

        context.Model.FindEntityType(typeof(ContactChannelAggregate)).ShouldBeNull();
    }
}
