using System.Diagnostics.CodeAnalysis;

namespace BusinessStatus.Domain.Enums;

/// <summary>
/// The two terminal stages of the catalogue, each identified by its reserved percentage:
/// <see cref="Won"/> is 100 and <see cref="Lost"/> is 0.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "There is no absent terminal kind: every caller asks for Won or Lost explicitly, " +
                    "and a zero member would be a state the domain cannot resolve.")]
public enum TerminalKind
{
    Won = 1,
    Lost = 2
}
