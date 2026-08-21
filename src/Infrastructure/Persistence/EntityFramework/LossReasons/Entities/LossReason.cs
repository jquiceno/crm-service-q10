namespace Infrastructure.Persistence.EntityFramework.LossReasons.Entities;

public sealed class LossReason
{
    public int Id { get; set; }

    // Nullable because the columns are. Reading a NULL into a non-nullable property makes
    // SqlClient fail the entire query, not the row.
    public string? Name { get; set; }

    public bool? IsActive { get; set; }
}
