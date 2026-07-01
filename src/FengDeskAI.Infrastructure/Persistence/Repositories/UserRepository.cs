using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
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
}
