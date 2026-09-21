using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Một user dùng một lần: có sẵn token, hỏng thì cũng không ảnh hưởng ca test khác.</summary>
public sealed record ThrowawayUser(Guid Id, string Email, string Password, string AccessToken);

/// <summary>
/// Tạo user dùng một lần cho các ca test có TÁC DỤNG PHỤ lên phiên đăng nhập.
///
/// Vì sao cần: <c>PATCH /api/admin/users/{id}/status</c>, <c>PUT .../roles</c>,
/// <c>POST .../revoke-sessions</c> và <c>POST /api/stores</c> đều tăng <c>TokenVersion</c> và thu hồi
/// refresh token của user bị tác động. Nhắm vào user mẫu dùng chung của <see cref="ApiTestFixture"/>
/// sẽ làm token của role đó chết, kéo theo mọi ca test chạy sau đó đỏ theo — lỗi rất khó lần ra vì
/// nó phụ thuộc thứ tự chạy. Mọi ca như vậy phải nhắm vào user dùng một lần lấy từ đây.
/// </summary>
public static class ScenarioUsers
{
    /// <summary>Tạo user mới (mặc định role Customer), đăng nhập thật để lấy access token.</summary>
    public static async Task<ThrowawayUser> CreateAsync(
        ApiTestFixture fixture,
        UserRole role = UserRole.Customer,
        bool isActive = true)
    {
        var email = ApiTestFixture.NewEmail();
        var password = TestSecrets.NewPassword();
        var userId = Guid.Empty;

        await fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var passwords = sp.GetRequiredService<IPasswordService>();

            var user = new User
            {
                Email = email,
                PasswordHash = passwords.Hash(password),
                FullName = "Throwaway Test User",
                Role = role,
                Gender = Gender.Unspecified,
                IsActive = isActive,
            };
            await db.Set<User>().AddAsync(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        });

        // User bị khóa thì không đăng nhập được — trả token rỗng, ca test chỉ cần Id.
        var token = isActive ? await LoginAsync(fixture, email, password) : string.Empty;
        return new ThrowawayUser(userId, email, password, token);
    }

    /// <summary>HttpClient mang token của user dùng một lần.</summary>
    public static HttpClient ClientFor(ApiTestFixture fixture, ThrowawayUser user)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return client;
    }

    /// <summary>Đăng nhập lại — dùng khi ca test vừa làm token cũ hết hiệu lực và cần token mới.</summary>
    public static Task<string> LoginAsync(ApiTestFixture fixture, ThrowawayUser user)
        => LoginAsync(fixture, user.Email, user.Password);

    private static async Task<string> LoginAsync(ApiTestFixture fixture, string email, string password)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/Auth/login", new { email, password });
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Đăng nhập user dùng một lần thất bại ({email}): {(int)response.StatusCode} {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }
}
