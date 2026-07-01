using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Vendor;
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

    public Task<List<GardenStore>> GetWithAddressByIdsAsync(IEnumerable<Guid> storeIds, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Where(s => storeIds.Contains(s.Id))
               .Include(s => s.Address).ThenInclude(a => a!.Ward).ThenInclude(w => w.District).ThenInclude(d => d.Province)
               .ToListAsync(ct);

    public async Task<bool> CanManageAsync(Guid storeId, Guid userId, CancellationToken ct = default)
    {
        if (await IsOwnerAsync(storeId, userId, ct)) return true;
        // Chỉ Accepted mới có quyền — Pending/Rejected/Revoked đều KHÔNG có quyền (đã thống nhất trong invitation flow).
        return await _context.Set<GardenStaffAssignment>()
            .AnyAsync(a => a.GardenStoreId == storeId && a.StaffId == userId && a.Status == InvitationStatus.Accepted, ct);
    }

    public Task<bool> IsOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>()
            .AnyAsync(o => o.GardenStoreId == storeId && o.OwnerUserId == userId, ct);

    public Task<List<GardenStore>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(s => s.Owners.Any(o => o.OwnerUserId == ownerUserId))
            .Include(s => s.Address).ThenInclude(a => a!.Ward)
            .Include(s => s.Owners)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public Task<List<GardenStore>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
            // Owner (mọi quan hệ sở hữu) HOẶC nhân viên đã Accepted — Pending/Rejected/Revoked KHÔNG được vào seller.
            .Where(s => s.Owners.Any(o => o.OwnerUserId == userId)
                || _context.Set<GardenStaffAssignment>().Any(a =>
                       a.GardenStoreId == s.Id
                       && a.StaffId == userId
                       && a.Status == InvitationStatus.Accepted))
            .Include(s => s.Address).ThenInclude(a => a!.Ward)
            .Include(s => s.Owners)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

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

    public Task<List<StaffAssignmentResponse>> GetStaffAsync(Guid storeId, CancellationToken ct = default)
    {
        // Trả về cả Pending + Accepted (để owner nhìn thấy lời mời chưa phản hồi).
        // Rejected/Revoked ẩn để giữ list gọn — owner muốn xem lịch sử có thể mở endpoint riêng.
        var users = _context.Set<User>().IgnoreQueryFilters();
        return _context.Set<GardenStaffAssignment>().AsNoTracking()
            .Where(a => a.GardenStoreId == storeId
                && (a.Status == InvitationStatus.Pending || a.Status == InvitationStatus.Accepted))
            .OrderByDescending(a => a.InvitedAt)
            .Select(a => new StaffAssignmentResponse
            {
                Id = a.Id,
                GardenStoreId = a.GardenStoreId,
                StaffId = a.StaffId,
                StaffName = users.Where(u => u.Id == a.StaffId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                StaffEmail = users.Where(u => u.Id == a.StaffId).Select(u => u.Email).FirstOrDefault() ?? string.Empty,
                StaffPhone = users.Where(u => u.Id == a.StaffId).Select(u => u.Phone).FirstOrDefault(),
                InvitedBy = a.InvitedBy,
                InvitedByName = users.Where(u => u.Id == a.InvitedBy).Select(u => u.FullName).FirstOrDefault(),
                Status = a.Status,
                InvitedAt = a.InvitedAt,
                RespondedAt = a.RespondedAt,
                UnassignedAt = a.UnassignedAt,
            })
            .ToListAsync(ct);
    }

    public Task<GardenStaffAssignment?> GetActiveAssignmentAsync(Guid storeId, Guid staffId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.GardenStoreId == storeId && a.StaffId == staffId
                && (a.Status == InvitationStatus.Pending || a.Status == InvitationStatus.Accepted), ct);

    public Task<GardenStaffAssignment?> GetAssignmentByIdAsync(Guid assignmentId, Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.GardenStoreId == storeId, ct);

    public Task<GardenStaffAssignment?> GetAssignmentByIdForUserAsync(Guid assignmentId, Guid staffUserId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StaffId == staffUserId, ct);

    public Task<List<InvitationResponse>> GetPendingInvitationsForUserAsync(Guid staffUserId, CancellationToken ct = default)
    {
        var users = _context.Set<User>().IgnoreQueryFilters();
        var stores = _set.IgnoreQueryFilters();
        return _context.Set<GardenStaffAssignment>().AsNoTracking()
            .Where(a => a.StaffId == staffUserId && a.Status == InvitationStatus.Pending)
            .OrderByDescending(a => a.InvitedAt)
            .Select(a => new InvitationResponse
            {
                Id = a.Id,
                GardenStoreId = a.GardenStoreId,
                StoreName = stores.Where(s => s.Id == a.GardenStoreId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                InvitedBy = a.InvitedBy,
                InvitedByName = users.Where(u => u.Id == a.InvitedBy).Select(u => u.FullName).FirstOrDefault(),
                Status = a.Status,
                InvitedAt = a.InvitedAt,
            })
            .ToListAsync(ct);
    }

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
