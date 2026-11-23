using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduPlatform.Persistence.Repositories;

/// <summary>
/// Coordinates ApplicationDbContext saves and optional explicit transactions.
/// Dispatches domain events after each SaveChangesAsync so handlers remain
/// decoupled from persistence details. See Ch.07 §7.3 for design rationale.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null) return; // idempotent — already in a transaction
        _transaction = await _db.Database.BeginTransactionAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Collect domain events BEFORE save (pre-save state is still available).
        var eventEntities = _db.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = eventEntities
            .SelectMany(e => e.DomainEvents)
            .OrderBy(e => e.OccurredOnUtc)
            .ToList();

        var result = await _db.SaveChangesAsync(ct);

        // Dispatch events after DB write but before transaction commit.
        // See Ch.07 §7.3 — acceptable trade-off for LMS; financial systems
        // should dispatch after CommitAsync.
        foreach (var evt in domainEvents)
            await _mediator.Publish(evt, ct);

        foreach (var entity in eventEntities)
            entity.ClearDomainEvents();

        return result;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
