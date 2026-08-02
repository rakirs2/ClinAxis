using Microsoft.Extensions.Logging;

namespace Scrapers.Tests.Helpers;

/// <summary>
/// In-memory <see cref="ILogger{T}"/> that captures entries for assertion.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<CapturedLog> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new CapturedLog(logLevel, eventId, formatter(state, exception), exception));
    }

    public sealed record CapturedLog(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
