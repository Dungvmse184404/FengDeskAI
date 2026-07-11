using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class UserAddressRepository : GenericRepository<UserAddress>, IUserAddressRepository
{
    public UserAddressRepository(AppDbContext context) : base(context) { }

    public Task<List<UserAddress>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(a => a.UserId == userId)
               .OrderByDescending(a => a.IsDefault)
               .ThenByDescending(a => a.UpdatedAt)
               .ToListAsync(ct);

    public Task<UserAddress?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public Task<UserAddress?> GetDefaultForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, ct);

    public Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default)
        // ExecuteUpdate chạy NGAY → bỏ default cũ trước khi set default mới, tránh vỡ partial-unique index.
        => _set.Where(a => a.UserId == userId && a.IsDefault)
               .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    public Task<UserAddress?> GetWithWardChainAsync(Guid id, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Include(a => a.Ward).ThenInclude(w => w.District).ThenInclude(d => d.Province)
               .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<UserAddress>> GetByUserIdWithWardChainAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Where(a => a.UserId == userId)
               .Include(a => a.Ward).ThenInclude(w => w.District).ThenInclude(d => d.Province)
               .OrderByDescending(a => a.IsDefault)
               .ThenByDescending(a => a.UpdatedAt)
               .ToListAsync(ct);
}
