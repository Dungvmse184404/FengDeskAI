using System.Linq.Expressions;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _set;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public virtual Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => _set.FindAsync(new[] { id }, ct).AsTask();

    public virtual Task<List<T>> GetAllAsync(CancellationToken ct = default)
        => _set.AsNoTracking().ToListAsync(ct);

    public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public virtual Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _set.AnyAsync(predicate, ct);

    public virtual Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null ? _set.CountAsync(ct) : _set.CountAsync(predicate, ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => await _set.AddRangeAsync(entities, ct);

    public virtual void Update(T entity) => _set.Update(entity);

    public virtual void Remove(T entity) => _set.Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);

    public virtual IQueryable<T> GetAllQueryable() => _set.AsQueryable();
}
