namespace Platform.Application.Abstractions.Time;

/// Abstraction over system clock to keep services testable. Concrete impl
/// lives in Infrastructure (SystemClock).
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
    DateOnly Today { get; }
}
