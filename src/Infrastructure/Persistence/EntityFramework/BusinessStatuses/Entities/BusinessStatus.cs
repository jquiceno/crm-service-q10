namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;

public sealed class BusinessStatus
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public decimal? Percentage { get; set; }

    public string? Color { get; set; }
}
