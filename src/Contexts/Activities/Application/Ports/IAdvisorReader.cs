namespace Activities.Application.Ports;

/// <summary>
/// Resolves an advisor's person code from the identification number the public contract carries.
/// </summary>
/// <remarks>
/// Same shape as <c>IPersonNameReader.GetFullNameAsync</c> in the template: a Reader that finds
/// nothing returns <c>null</c> instead of a failed <c>Result</c>.
/// <para>
/// This port deliberately does <b>not</b> validate the advisor's role. That rule belongs to the
/// Security domain and is the caller's responsibility (DEC-17); during phase 1 the monolith
/// adapter keeps its existing check before delegating.
/// </para>
/// </remarks>
public interface IAdvisorReader
{
    Task<string?> ResolveByIdentificationAsync(
        string? identification,
        CancellationToken cancellationToken = default);
}
