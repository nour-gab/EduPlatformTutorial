using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Repositories;

/// <summary>
/// Generic EF Core repository that implements both IRepository&lt;T&gt; (read+write)
/// and IReadRepository&lt;T&gt; (read-only). All query filtering is delegated to
/// Ardalis.Specification so EF Core details never leak into Application handlers.
/// </summary>
public class Repository<T> : IRepository<T>, IReadRepository<T>
    where T : class
{
    private readonly ApplicationDbContext _db;
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    // ── Read operations ───────────────────────────────────────────────────────

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync(new object[] { id }, ct);

    public async Task<T?> FirstOrDefaultAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).FirstOrDefaultAsync(ct);

    public async Task<List<T>> ListAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).ToListAsync(ct);

    public async Task<int> CountAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).CountAsync(ct);

    // ── Write operations ──────────────────────────────────────────────────────

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        var entry = await _set.AddAsync(entity, ct);
        return entry.Entity;
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _set.Remove(entity);
        return Task.CompletedTask;
    }
}
