using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class StoreRepository : GenericRepository<GardenStore>, IStoreRepository
{
    public StoreRepository(AppDbContext context) : base(context) { }

    public Task<List<GardenStore>> GetActiveAsync(CancellationToken ct = default)
        => _set.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);

    public Task<GardenStore?> GetDetailAsync(Guid id, CancellationToken ct = default)
        => _set.Include(s => s.Address).ThenInclude(a => a!.Ward)
               .Include(s => s.Owners)
               .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<bool> CanManageAsync(Guid storeId, Guid userId, CancellationToken ct = default)
    {
        if (await IsOwnerAsync(storeId, userId, ct)) return true;
        return await _context.Set<GardenStaffAssignment>()
            .AnyAsync(a => a.GardenStoreId == storeId && a.StaffId == userId && a.IsActive, ct);
    }

    public Task<bool> IsOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>()
            .AnyAsync(o => o.GardenStoreId == storeId && o.OwnerUserId == userId, ct);

    public Task<List<GardenStoreOwner>> GetOwnersAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>().AsNoTracking()
            .Where(o => o.GardenStoreId == storeId)
            .OrderByDescending(o => o.IsPrimary).ThenBy(o => o.AssignedAt)
            .ToListAsync(ct);

    public Task<GardenStoreOwner?> GetOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>()
            .FirstOrDefaultAsync(o => o.GardenStoreId == storeId && o.OwnerUserId == userId, ct);

    public Task<int> CountOwnersAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>().CountAsync(o => o.GardenStoreId == storeId, ct);

    public async Task AddOwnerAsync(GardenStoreOwner owner, CancellationToken ct = default)
        => await _context.Set<GardenStoreOwner>().AddAsync(owner, ct);

    public Task<List<GardenStaffAssignment>> GetStaffAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>().AsNoTracking()
            .Where(a => a.GardenStoreId == storeId && a.IsActive)
            .OrderByDescending(a => a.AssignedAt).ToListAsync(ct);

    public Task<GardenStaffAssignment?> GetActiveAssignmentAsync(Guid storeId, Guid staffId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.GardenStoreId == storeId && a.StaffId == staffId && a.IsActive, ct);

    public Task<GardenStaffAssignment?> GetAssignmentByIdAsync(Guid assignmentId, Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.GardenStoreId == storeId, ct);

    public async Task AddAssignmentAsync(GardenStaffAssignment assignment, CancellationToken ct = default)
        => await _context.Set<GardenStaffAssignment>().AddAsync(assignment, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _set.IgnoreQueryFilters().AnyAsync(s => s.Id == id, ct);

    public Task<StoreAddress?> GetAddressAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);

    public Task<StoreAddress?> GetAddressIncludingDeletedAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().IgnoreQueryFilters().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);

    public Task<bool> AddressExistsAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().IgnoreQueryFilters().AnyAsync(a => a.StoreId == storeId, ct);

    public async Task AddAddressAsync(StoreAddress address, CancellationToken ct = default)
        => await _context.Set<StoreAddress>().AddAsync(address, ct);

    public async Task HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Bypass soft-delete interceptor (SaveChanges) bằng ExecuteDelete chạy SQL trực tiếp.
        await _context.Set<StoreAddress>().IgnoreQueryFilters()
            .Where(a => a.StoreId == id).ExecuteDeleteAsync(ct);
        await _context.Set<GardenStaffAssignment>().IgnoreQueryFilters()
            .Where(a => a.GardenStoreId == id).ExecuteDeleteAsync(ct);
        await _context.Set<GardenStoreOwner>().IgnoreQueryFilters()
            .Where(o => o.GardenStoreId == id).ExecuteDeleteAsync(ct);
        await _set.IgnoreQueryFilters()
            .Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task HardDeleteAddressAsync(Guid storeId, CancellationToken ct = default)
        => await _context.Set<StoreAddress>().IgnoreQueryFilters()
            .Where(a => a.StoreId == storeId).ExecuteDeleteAsync(ct);
}
