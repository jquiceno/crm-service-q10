namespace Infrastructure.Persistence.EntityFramework.AdsChannels.Entities;

// Database First over the legacy tbl_opo_medios_publicitarios table: nullability here reflects the
// real column definitions, not the domain's requirements (see AdsChannelAggregate for those).
public sealed class AdsChannel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}
