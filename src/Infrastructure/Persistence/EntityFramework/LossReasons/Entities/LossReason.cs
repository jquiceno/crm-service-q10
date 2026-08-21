namespace Infrastructure.Persistence.EntityFramework.LossReasons.Entities;

public sealed class LossReason
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
