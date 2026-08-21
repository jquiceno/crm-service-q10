using BusinessStatus.Domain.Enums;

namespace BusinessStatus.Domain.Queries;

/// <summary>
/// Optional filters of the catalogue listing. A null member means "no filter", which is the
/// semantics of the legacy stored procedure and not the one of the legacy form.
/// </summary>
public sealed record BusinessStatusFilter(string? Name, bool? IsActive, BusinessStatusKind Kind);
