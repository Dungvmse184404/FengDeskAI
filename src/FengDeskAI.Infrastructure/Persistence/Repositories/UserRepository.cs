using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(u => u.Phone == phone, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _set.AnyAsync(u => u.Email == email, ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
        => _set.AnyAsync(u => u.Phone == phone, ct);

    public Task<List<User>> SearchAsync(Guid searcherId, string normalizedQuery, int limit, CancellationToken ct = default)
    {
        var pattern = $"%{normalizedQuery}%";
        return _set.AsNoTracking()
            .Where(u => u.IsActive && u.Id != searcherId)
            .Where(u =>
                EF.Functions.Like(
                    AppDbContext.Unaccent(u.FullName).Replace("đ", "d").Replace("Đ", "d").ToLower(),
                    pattern)
                || EF.Functions.Like(u.Email.ToLower(), pattern)
                || (u.Phone != null && EF.Functions.Like(u.Phone.ToLower(), pattern)))
            .OrderBy(u => u.FullName)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<(List<User> Items, int Total)> GetAdminPageAsync(
        int skip, int take, string? query, UserRole? role, bool? isActive, CancellationToken ct = default)
    {
        var source = _set.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim().ToLower()}%";
            source = source.Where(u =>
                EF.Functions.Like(u.Email.ToLower(), pattern)
                || EF.Functions.Like(u.FullName.ToLower(), pattern)
                || (u.Phone != null && EF.Functions.Like(u.Phone, pattern)));
        }
        if (role is { } roleFilter)
            source = source.Where(u => (u.Role & roleFilter) == roleFilter);
        if (isActive.HasValue)
            source = source.Where(u => u.IsActive == isActive.Value);

        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(u => u.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken ct = default)
        => _set.CountAsync(u => u.IsActive && (u.Role & UserRole.Admin) == UserRole.Admin, ct);
}
