using ContactChannel.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Mappers;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.ContactChannels;

public sealed class ContactChannelRepositoryMapperTests
{
    [Fact]
    public void ToDomain_CopiesTheRowIntoTheAggregate()
    {
        var document = new ContactChannelEntity { Id = 7, Name = "WhatsApp", IsActive = true };

        var aggregate = ContactChannelRepositoryMapper.ToDomain(document);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBe("WhatsApp");
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToDomain_WithAnInactiveRow_KeepsTheStateFalse()
    {
        var document = new ContactChannelEntity { Id = 7, Name = "WhatsApp", IsActive = false };

        ContactChannelRepositoryMapper.ToDomain(document).IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ToDomain_DoesNotRevalidateThePersistedRow()
    {
        var document = new ContactChannelEntity { Id = 7, Name = new string('a', 500), IsActive = true };

        ContactChannelRepositoryMapper.ToDomain(document).Name.Length.ShouldBe(500);
    }

    [Fact]
    public void ToDocument_CopiesTheAggregateIntoTheRow()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: "WhatsApp", isActive: true);

        var document = ContactChannelRepositoryMapper.ToDocument(aggregate);

        document.Id.ShouldBe(7);
        document.Name.ShouldBe("WhatsApp");
        document.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToDocument_OfANewAggregate_LeavesTheIdentifierUnassigned()
    {
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        ContactChannelRepositoryMapper.ToDocument(aggregate.Value).Id.ShouldBe(0);
    }
}
