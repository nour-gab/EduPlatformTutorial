using MediatR;

namespace EduPlatform.Domain.Common;

/// <summary>
/// Marker interface for events raised by an aggregate root.
/// Extends INotification so MediatR can dispatch them directly after SaveChangesAsync.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}