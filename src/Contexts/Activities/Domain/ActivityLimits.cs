namespace Activities.Domain;

/// <summary>
/// Length limits of the domain, taken from the legacy columns they persist to.
/// </summary>
/// <remarks>
/// <see cref="OutcomeMaxLength"/> is intentionally absent: the logical contract of the outcome
/// text is <c>varchar(MAX)</c> and the domain imposes no cap (DEC-3). The 2000-character limit
/// of the divergent tenants is enforced at the API edge during phase 1.
/// </remarks>
public static class ActivityLimits
{
    /// <summary>Persisted to <c>negact_titulo varchar(500)</c>.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Persisted to <c>negact_asesor</c> / <c>negact_per_codigo varchar(20)</c>.</summary>
    public const int AdvisorIdMaxLength = 20;
}
