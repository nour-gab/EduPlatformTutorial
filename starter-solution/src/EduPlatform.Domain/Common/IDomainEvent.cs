namespace EduPlatform.Domain.Common;

/// <summary>
/// Marker interface for events raised by an aggregate root.
/// The Application layer converts these to MediatR INotifications after SaveChangesAsync.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}