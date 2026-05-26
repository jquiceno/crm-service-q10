namespace Shared.Application.Interfaces;

/// <summary>
/// Extension point for pushing typed flat properties to the log context at root level.
/// Implement this interface to enrich logs with domain-specific context without nesting.
/// Use <c>LogContextExtensions.PushFlatProperties</c> to activate the scope.
/// </summary>
public interface ILogProperties
{
    IEnumerable<KeyValuePair<string, object?>> GetProperties();
}
