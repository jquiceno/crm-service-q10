using Serilog;
using Shared.Application.Interfaces;

namespace Infrastructure.Logging;

public class SerilogLogger<T> : ILoggerService<T>
{
    private readonly ILogger _logger = Log.ForContext<T>();

    public void Debug(string message, params object[] args)
        => _logger.Debug(message, args);

    public void Info(string message, params object[] args)
        => _logger.Information(message, args);

    public void Warning(string message, params object[] args)
        => _logger.Warning(message, args);

    public void Error(Exception? exception, string message, params object[] args)
        => _logger.Error(exception, message, args);
}
