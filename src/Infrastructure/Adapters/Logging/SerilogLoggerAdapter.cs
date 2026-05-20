using Shared.Application.Ports;
using ILogger = Serilog.ILogger;

namespace Infrastructure.Adapters.Logging;

public sealed class SerilogLoggerAdapter<T> : ILoggerPort<T>
{
    private readonly ILogger _logger;

    public SerilogLoggerAdapter(ILogger logger) => _logger = logger.ForContext<T>();

    public void Debug(string message, params object[] args)
        => _logger.Debug(message, args);

    public void Info(string message, params object[] args)
        => _logger.Information(message, args);

    public void Warning(string message, params object[] args)
        => _logger.Warning(message, args);

    public void Warning(Exception? exception, string message, params object[] args)
        => _logger.Warning(exception, message, args);

    public void Error(Exception? exception, string message, params object[] args)
        => _logger.Error(exception, message, args);
}
