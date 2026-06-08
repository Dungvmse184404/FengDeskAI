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

    public async Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var defaults = await _set.Where(a => a.UserId == userId && a.IsDefault).ToListAsync(ct);
        foreach (var addr in defaults) addr.IsDefault = false;
    }
}
