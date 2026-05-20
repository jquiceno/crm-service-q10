namespace ServiceTemplate.Tests.Api.Doubles;

/// <summary>
/// Singleton decorator that delegates to a freshly-resolved scoped
/// <see cref="GetWeatherForecastUseCase"/> and counts actual executions, so
/// output-cache hits/misses can be asserted.
/// </summary>
internal sealed class CountingGetWeatherForecastUseCase(IServiceScopeFactory scopes) : IGetWeatherForecastPort
{
    private int _executions;

    public int Executions => _executions;

    public async Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _executions);

        using var scope = scopes.CreateScope();
        var inner = scope.ServiceProvider.GetRequiredService<GetWeatherForecastUseCase>();
        return await inner.ExecuteAsync(cancellationToken);
    }
}
