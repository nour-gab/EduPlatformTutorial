using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EduPlatform.Domain.Common
{
    /// <summary>
    /// Base class for all entities. Provides identity, audit fields,
    /// a rowversion for optimistic concurrency, and a domain events list.
    /// (Stub — full implementation is walked through in Ch.03.)
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
        public void ClearDomainEvents() => _domainEvents.Clear();
        public void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
    }

    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTime OccurredOnUtc { get; }
    }

    public abstract class DomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    }

    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception inner) : base(message, inner) { }
    }
}
