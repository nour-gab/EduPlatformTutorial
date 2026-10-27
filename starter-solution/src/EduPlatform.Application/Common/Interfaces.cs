using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;

namespace EduPlatform.Application.Common.Interfaces
{
    // Stubs — full implementations walked through in Ch.07.
    // These interfaces are declared in Application, implemented in Persistence.

    public interface IReadRepository<T> where T : class
    {
        Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
        Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
        Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
    }

    public interface IRepository<T> : IReadRepository<T> where T : class
    {
        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(T entity, CancellationToken ct = default);
        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    }

    public interface IUnitOfWork
    {
        Task BeginTransactionAsync(CancellationToken ct = default);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }

    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
        Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
        Task RemoveAsync(string key, CancellationToken ct = default);
    }
}
