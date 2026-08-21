namespace BusinessStatus.Domain.Enums;

/// <summary>
/// Stage filter of the catalogue listing. The legacy code repeated the same "exclude 0 and 100"
/// condition in seven places; here it is one value.
/// </summary>
public enum BusinessStatusKind
{
    All = 0,
    Intermediate = 1,
    Terminal = 2
}
