namespace Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;

public sealed class ContactChannelEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}
