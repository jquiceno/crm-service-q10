using Serilog.Context;
using Shared.Application.Interfaces;

namespace Infrastructure.Logging;

public static class LogContextExtensions
{
    public static IDisposable PushHttpProperties(this HttpRequestLogProperties properties)
        => LogContext.PushProperty("http", properties, destructureObjects: true);

    public static IDisposable PushHttpProperties(this HttpContextProperties properties)
        => LogContext.PushProperty("http", properties, destructureObjects: true);

    public static IDisposable PushProperties(this IReadOnlyDictionary<string, object?> properties)
        => LogContext.PushProperty("properties", properties, destructureObjects: true);

    public static IDisposable PushFlatProperties(this ILogProperties properties)
    {
        var disposables = properties
            .GetProperties()
            .Select(p => LogContext.PushProperty(p.Key, p.Value))
            .ToArray();

        return new CompositeDisposable(disposables);
    }

    private sealed class CompositeDisposable(IDisposable[] disposables) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            for (var i = disposables.Length - 1; i >= 0; i--)
                disposables[i].Dispose();
        }
    }
}