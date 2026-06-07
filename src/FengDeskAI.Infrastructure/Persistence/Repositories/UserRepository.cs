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
}
