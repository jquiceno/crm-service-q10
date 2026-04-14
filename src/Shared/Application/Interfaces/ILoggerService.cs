namespace Shared.Application.Interfaces;

public interface ILoggerService<T>
{
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Warning(Exception? exception, string message, params object[] args);
    void Error(Exception? exception, string message, params object[] args);
}
