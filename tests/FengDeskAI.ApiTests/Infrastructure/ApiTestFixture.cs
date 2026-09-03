using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using FengDeskAI.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Các role cần token để chạy ma trận phân quyền.</summary>
public enum TestRole
{
    Anonymous,
    Customer,
    Staff,
    Manager,
    Admin,
    GardenOwner,
}

/// <summary>
/// Dựng API + DB một lần cho cả bộ test (migrate, seed, tạo user mẫu mỗi role, đăng nhập lấy token).
/// Token lấy qua <c>POST /api/Auth/login</c> thật chứ không tự ký — để test đi đúng đường mà client đi.
/// </summary>
public sealed class ApiTestFixture : IAsyncLifetime
{
    /// <summary>Sinh mới mỗi lần chạy — không có mật khẩu nào nằm trong mã nguồn.</summary>
    private readonly string _password = TestSecrets.NewPassword();

    private readonly Dictionary<TestRole, string> _tokens = [];
    private readonly Dictionary<TestRole, Guid> _userIds = [];

    public ApiTestFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Mật khẩu của mọi user mẫu trong phiên chạy này. File dữ liệu tham chiếu qua {{password}}.</summary>
    public string Password => _password;

    /// <summary>Email chưa từng đăng ký, khác nhau mỗi lần gọi — dùng cho các ca đăng ký mới.</summary>
    public static string NewEmail() => $"tc-{Guid.NewGuid():N}@fengdesk.test";

    public async Task InitializeAsync()
    {
        Factory = new ApiTestFactory();
        Client = Factory.CreateClient();

        // Migrate + chạy toàn bộ IDataSeeder (gồm cả tài khoản admin@fengdesk.local).
        await Factory.Services.RunSeedersAsync();

        await SeedRoleUsersAsync();
        await LoginAllRolesAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>HttpClient đã gắn sẵn bearer token của role. <see cref="TestRole.Anonymous"/> = không token.</summary>
    public HttpClient ClientFor(TestRole role)
    {
        var client = Factory.CreateClient();
        if (role != TestRole.Anonymous)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokens[role]);

        return client;
    }

    public string TokenFor(TestRole role) => _tokens[role];

    /// <summary>Id của user mẫu — cần khi fixture dựng dữ liệu ghi thẳng vào DB.</summary>
    public Guid UserId(TestRole role) => _userIds[role];

    public static string EmailFor(TestRole role) => $"{role.ToString().ToLowerInvariant()}.test@fengdesk.local";

    /// <summary>Chạy một đoạn code với scope DI thật của app (đọc/ghi DB trực tiếp khi cần dựng dữ liệu).</summary>
    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    private async Task SeedRoleUsersAsync()
    {
        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var passwords = sp.GetRequiredService<IPasswordService>();

            foreach (var (role, userRole) in RoleMap())
            {
                var email = EmailFor(role);
                var hash = passwords.Hash(_password);

                var existing = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
                if (existing is not null)
                {
                    // Mật khẩu sinh mới mỗi lần chạy → user còn lại từ lần trước phải được đặt lại
                    // hash, nếu không sẽ đăng nhập thất bại ở lần chạy thứ hai trở đi.
                    existing.PasswordHash = hash;
                    existing.Role = userRole;
                    existing.IsActive = true;
                    continue;
                }

                var created = new User
                {
                    Email = email,
                    PasswordHash = hash,
                    FullName = $"Test {role}",
                    Role = userRole,
                    Gender = Gender.Unspecified,
                    IsActive = true,
                };
                await db.Set<User>().AddAsync(created);
            }

            await db.SaveChangesAsync();

            foreach (var role in RoleMap().Keys)
            {
                var email = EmailFor(role);
                _userIds[role] = await db.Set<User>().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            }
        });
    }

    private async Task LoginAllRolesAsync()
    {
        foreach (var role in RoleMap().Keys)
            _tokens[role] = await LoginAsync(EmailFor(role), _password);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/Auth/login", new { email, password });
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Đăng nhập test thất bại cho {email}: {(int)response.StatusCode} {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("accessToken").GetString()
               ?? throw new InvalidOperationException($"Response đăng nhập của {email} không có accessToken: {body}");
    }

    /// <summary>
    /// Test tự tạo user cho MỌI role, kể cả Admin — không mượn tài khoản của <c>AdminUserSeeder</c>,
    /// để mật khẩu mặc định của seeder không bị chép lại vào mã test.
    /// Phải khớp <c>EndpointCatalog.RoleClaims</c>.
    /// </summary>
    private static Dictionary<TestRole, UserRole> RoleMap() => new()
    {
        [TestRole.Customer] = UserRole.Customer,
        [TestRole.Staff] = UserRole.Staff,
        [TestRole.Manager] = UserRole.Manager,
        [TestRole.Admin] = UserRole.Admin,
        // GardenOwner là flag cộng thêm, không thay thế Customer.
        [TestRole.GardenOwner] = UserRole.Customer | UserRole.GardenOwner,
    };
}

/// <summary>Gom mọi test vào một collection để chỉ dựng API + DB một lần.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "api";
}
