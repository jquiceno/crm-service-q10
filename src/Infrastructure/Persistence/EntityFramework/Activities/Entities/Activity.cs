namespace Infrastructure.Persistence.EntityFramework.Activities.Entities;

/// <summary>
/// Persistence shape of one <c>tbl_opo_negocios_actividades</c> row: raw legacy columns, no
/// domain types. This is what the <c>DbSet</c> maps — the aggregate never touches EF; the
/// translation in both directions lives in the repository mapper.
/// </summary>
internal sealed class Activity
{
    public int Id { get; set; }

    public int DealId { get; set; }

    public int? OpportunityId { get; set; }

    /// <summary>Raw <c>negact_tipo</c> char ('1'..'7').</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Raw <c>negact_titulo</c> — the domain's Description (§4: inverted on purpose).</summary>
    public string? Title { get; set; }

    /// <summary>Raw <c>negact_descripcion</c> — the domain's Outcome text (§4: inverted on purpose).</summary>
    public string? OutcomeText { get; set; }

    /// <summary>Raw <c>negact_resultado</c> char, whose meaning depends on <see cref="Type"/>.</summary>
    public string? OutcomeCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DueAt { get; set; }

    public bool? IsCompleted { get; set; }

    public bool? IsCancelled { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdvisorId { get; set; }

    public string CreatedById { get; set; } = string.Empty;
}
