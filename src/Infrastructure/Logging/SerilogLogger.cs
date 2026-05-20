using Serilog;
using Shared.Application.Interfaces;
using ILogger = Serilog.ILogger;

namespace Infrastructure.Logging;

public sealed class SerilogLogger<T> : ILoggerService<T>
{
    private readonly ILogger _logger;

    public SerilogLogger(ILogger logger) => _logger = logger.ForContext<T>();

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