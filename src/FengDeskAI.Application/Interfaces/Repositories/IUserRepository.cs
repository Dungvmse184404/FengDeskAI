using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// Tìm user theo email / fullName (không phân biệt dấu) / phone — kiểu GitHub.
    /// <paramref name="normalizedQuery"/> đã được lowercase + bỏ dấu + đ→d phía Application để khớp với SQL.
    /// </summary>
    Task<List<User>> SearchAsync(Guid searcherId, string normalizedQuery, int limit, CancellationToken ct = default);
    Task<(List<User> Items, int Total)> GetAdminPageAsync(
        int skip, int take, string? query, UserRole? role, bool? isActive, CancellationToken ct = default);
    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);
}
