using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

public class AdminUserSeeder : IDataSeeder
{
    private const string AdminEmail = "admin@fengdesk.local";
    private const string AdminPassword = "Admin@123";

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwords;
    private readonly ILogger<AdminUserSeeder> _logger;

    public AdminUserSeeder(AppDbContext context, IPasswordService passwords, ILogger<AdminUserSeeder> logger)
    {
        _context = context;
        _passwords = passwords;
        _logger = logger;
    }

    public int Order => 5;
    public string Name => "Seed default admin account";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);

        if (existing is null)
        {
            var admin = new User
            {
                Email = AdminEmail,
                PasswordHash = _passwords.Hash(AdminPassword),
                FullName = "FengDeskAI Admin",
                Gender = Gender.Unspecified,
                Role = UserRole.Admin,
                IsActive = true,
            };

            await _context.Set<User>().AddAsync(admin, ct);
            await _context.SaveChangesAsync(ct);

            // Do not log plaintext password in production logs. Log only that admin was created.
            _logger.LogInformation("Seeded admin account {Email}.", AdminEmail);
            return;
        }

        var updated = false;
        if (!existing.Role.Has(UserRole.Admin))
        {
            existing.Role = existing.Role.Add(UserRole.Admin);
            updated = true;
        }

        // Tài khoản admin có thể từng được tạo qua Google nên chưa có password hash.
        if (string.IsNullOrWhiteSpace(existing.PasswordHash)
            || !_passwords.Verify(AdminPassword, existing.PasswordHash))
        {
            existing.PasswordHash = _passwords.Hash(AdminPassword);
            updated = true;
        }

        if (!existing.IsActive)
        {
            existing.IsActive = true;
            updated = true;
        }

        if (updated)
        {
            _context.Set<User>().Update(existing);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Updated existing user {Email} to be admin and set default password.", AdminEmail);
        }
        else
        {
            _logger.LogInformation("Admin account {Email} already exists and is unchanged.", AdminEmail);
        }
    }
}
