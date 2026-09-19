using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Domain.Enums;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 6 — Quản trị người dùng: tra cứu, khóa/mở tài khoản, đổi vai trò, thu hồi phiên đăng nhập,
/// nhật ký kiểm toán — kèm các chốt tự bảo vệ (admin không tự khóa, không tự gỡ quyền admin).
///
/// Bẫy phải biết: cả ba thao tác ghi (<c>status</c>, <c>roles</c>, <c>revoke-sessions</c>) đều tăng
/// <c>TokenVersion</c> và thu hồi refresh token của NGƯỜI BỊ TÁC ĐỘNG. Nhắm vào user mẫu dùng chung
/// của fixture sẽ làm token role đó chết và kéo mọi ca test sau đỏ theo — lỗi phụ thuộc thứ tự chạy,
/// rất khó lần. Vì thế mọi ca ở đây nhắm vào user dùng một lần (<see cref="ScenarioUsers"/>).
///
/// Chốt "admin cuối cùng" (<c>409</c>) KHÔNG phủ được ở tầng này: <c>AdminUserSeeder</c> tạo sẵn
/// <c>admin@fengdesk.local</c>, cộng với admin của fixture là đã có ≥ 2 admin đang hoạt động nên
/// nhánh đó không bao giờ chạm tới. Muốn phủ thì phải hạ hết admin khác trong cùng một ca — làm vậy
/// sẽ phá fixture dùng chung, nên để lại cho unit test của <c>AdminUserService</c>.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AdminFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AdminFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Tra cứu =====================

    [Fact(DisplayName = "ADMIN-01 [Normal] An admin lists users and gets a paged result")]
    public async Task ListUsers_AsAdmin_ReturnsPagedResult()
    {
        var response = await Admin().GetAsync("/api/admin/users?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(1, data.GetProperty("page").GetInt32());
        Assert.Equal(5, data.GetProperty("pageSize").GetInt32());
        Assert.True(data.GetProperty("totalCount").GetInt32() > 0);
        Assert.True(data.GetProperty("items").GetArrayLength() <= 5);
    }

    [Fact(DisplayName = "ADMIN-02 [Boundary] An out-of-range page size falls back to the default")]
    public async Task ListUsers_PageSizeAboveMax_FallsBackToDefault()
    {
        var response = await Admin().GetAsync("/api/admin/users?page=0&pageSize=9999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(1, data.GetProperty("page").GetInt32());
        Assert.True(data.GetProperty("pageSize").GetInt32() <= 100,
            "pageSize vượt trần phải bị kẹp lại, không được truyền thẳng xuống DB.");
    }

    [Fact(DisplayName = "ADMIN-03 [Normal] Searching by email finds the matching user")]
    public async Task ListUsers_FilteredByEmail_FindsUser()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().GetAsync($"/api/admin/users?query={Uri.EscapeDataString(user.Email)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.GetProperty("items").EnumerateArray(), u => u.GetProperty("id").GetGuid() == user.Id);
    }

    [Fact(DisplayName = "ADMIN-04 [Normal] Filtering by role only returns users carrying that role")]
    public async Task ListUsers_FilteredByRole_ReturnsOnlyThatRole()
    {
        await ScenarioUsers.CreateAsync(_fixture, UserRole.Customer | UserRole.GardenOwner);

        var response = await Admin().GetAsync("/api/admin/users?role=GardenOwner&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.All(data.GetProperty("items").EnumerateArray(), u =>
            Assert.Contains(u.GetProperty("roles").EnumerateArray(), r => r.GetString() == "GardenOwner"));
    }

    [Fact(DisplayName = "ADMIN-05 [Normal] Reading a single user returns their roles as a list")]
    public async Task GetUser_AsAdmin_ReturnsRoleList()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().GetAsync($"/api/admin/users/{user.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(user.Email, data.GetProperty("email").GetString());
        Assert.Contains(data.GetProperty("roles").EnumerateArray(), r => r.GetString() == "Customer");
    }

    [Fact(DisplayName = "ADMIN-06 [Abnormal] Reading an unknown user returns 404")]
    public async Task GetUser_UnknownId_ReturnsNotFound()
    {
        var response = await Admin().GetAsync($"/api/admin/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-07 [Abnormal] A manager cannot reach the admin user endpoints")]
    public async Task ListUsers_AsManager_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Manager).GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Khóa / mở tài khoản =====================

    [Fact(DisplayName = "ADMIN-08 [Normal] Locking a user blocks their subsequent login")]
    public async Task LockUser_AsAdmin_BlocksLogin()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status",
            new { isActive = false, reason = "Vi phạm điều khoản." });
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await ApiEnvelope.DataAsync(response)).GetProperty("isActive").GetBoolean());

        var login = await _fixture.Client.PostAsJsonAsync("/api/Auth/login",
            new { email = user.Email, password = user.Password });
        Assert.False(login.IsSuccessStatusCode, "Tài khoản đã khóa thì không được đăng nhập.");
    }

    [Fact(DisplayName = "ADMIN-09 [Normal] Locking a user invalidates the access token they already hold")]
    public async Task LockUser_InvalidatesExistingAccessToken()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var beforeLock = ScenarioUsers.ClientFor(_fixture, user);
        Assert.True((await beforeLock.GetAsync("/api/Auth/me")).IsSuccessStatusCode,
            "Token phải dùng được TRƯỚC khi khóa, nếu không phép so sánh sau đây vô nghĩa.");

        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status", new { isActive = false });

        var afterLock = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-10 [Normal] Unlocking a user lets them log in again")]
    public async Task UnlockUser_AsAdmin_RestoresLogin()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status", new { isActive = false });

        var unlock = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);

        var login = await _fixture.Client.PostAsJsonAsync("/api/Auth/login",
            new { email = user.Email, password = user.Password });
        Assert.True(login.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(login, "đăng nhập lại sau khi mở khóa"));
    }

    [Fact(DisplayName = "ADMIN-11 [Normal] Setting the status to its current value is a no-op")]
    public async Task UpdateStatus_SameValue_IsNoOp()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var before = await TokenVersionAsync(user.Id);

        var response = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status", new { isActive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await TokenVersionAsync(user.Id));
    }

    [Fact(DisplayName = "ADMIN-12 [Abnormal] An admin cannot lock their own account")]
    public async Task LockUser_Self_IsRejected()
    {
        var adminId = _fixture.UserId(TestRole.Admin);

        var response = await Admin().PatchAsJsonAsync($"/api/admin/users/{adminId}/status",
            new { isActive = false, reason = "Thử tự khóa." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tự khóa", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "ADMIN-13 [Abnormal] Changing the status of an unknown user returns 404")]
    public async Task UpdateStatus_UnknownUser_ReturnsNotFound()
    {
        var response = await Admin().PatchAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Vai trò =====================

    [Fact(DisplayName = "ADMIN-14 [Normal] Granting a role is reflected when the user reads it back")]
    public async Task UpdateRoles_AsAdmin_PersistsNewRoles()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",
            new { roles = new[] { "Customer", "GardenOwner" }, reason = "Duyệt làm chủ vườn." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = (await ApiEnvelope.DataAsync(response)).GetProperty("roles")
            .EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains("Customer", roles);
        Assert.Contains("GardenOwner", roles);
    }

    [Fact(DisplayName = "ADMIN-15 [Normal] A new role takes effect on the next login")]
    public async Task UpdateRoles_TakesEffectAfterRelogin()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await Admin().PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",
            new { roles = new[] { "Customer", "GardenOwner" } });

        var refreshed = user with { AccessToken = await ScenarioUsers.LoginAsync(_fixture, user) };
        var response = await ScenarioUsers.ClientFor(_fixture, refreshed)
            .PostAsJsonAsync("/api/tags", new { name = $"tag-{Guid.NewGuid():N}"[..14] });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-16 [Abnormal] An empty role list is rejected")]
    public async Task UpdateRoles_EmptyList_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",
            new { roles = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("role", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "ADMIN-17 [Abnormal] A role list containing None is rejected")]
    public async Task UpdateRoles_ContainingNone_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",
            new { roles = new[] { "None" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-18 [Abnormal] An admin cannot strip their own admin role")]
    public async Task UpdateRoles_SelfDemotion_IsRejected()
    {
        var adminId = _fixture.UserId(TestRole.Admin);

        var response = await Admin().PutAsJsonAsync($"/api/admin/users/{adminId}/roles",
            new { roles = new[] { "Customer" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Admin", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    // ===================== Thu hồi phiên =====================

    [Fact(DisplayName = "ADMIN-19 [Normal] Revoking sessions kills the token the user already holds")]
    public async Task RevokeSessions_AsAdmin_KillsExistingToken()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        Assert.True((await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/Auth/me")).IsSuccessStatusCode);

        var revoke = await Admin().PostAsJsonAsync($"/api/admin/users/{user.Id}/revoke-sessions",
            new { reason = "Nghi ngờ lộ mật khẩu." });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var afterRevoke = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-20 [Normal] Revoking sessions works without a request body")]
    public async Task RevokeSessions_WithoutBody_Succeeds()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await Admin().PostAsync($"/api/admin/users/{user.Id}/revoke-sessions", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-21 [Normal] The user can log in again after their sessions were revoked")]
    public async Task RevokeSessions_DoesNotLockTheAccount()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await Admin().PostAsync($"/api/admin/users/{user.Id}/revoke-sessions", null);

        var login = await _fixture.Client.PostAsJsonAsync("/api/Auth/login",
            new { email = user.Email, password = user.Password });

        Assert.True(login.IsSuccessStatusCode,
            "Thu hồi phiên chỉ đá phiên cũ, không được khóa tài khoản.");
    }

    // ===================== Nhật ký kiểm toán =====================

    [Fact(DisplayName = "ADMIN-22 [Normal] A status change writes an audit entry naming the actor")]
    public async Task AuditLogs_AfterStatusChange_RecordActor()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status",
            new { isActive = false, reason = "Ghi nhật ký." });

        var response = await Admin().GetAsync($"/api/admin/users/{user.Id}/audit-logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ApiEnvelope.DataAsync(response)).GetProperty("items");
        var entry = items.EnumerateArray().FirstOrDefault(e => e.GetProperty("action").GetString() == "UpdateUserStatus");
        Assert.NotEqual(JsonValueKind.Undefined, entry.ValueKind);
        Assert.Equal(_fixture.UserId(TestRole.Admin), entry.GetProperty("actorUserId").GetGuid());
        Assert.Equal("Ghi nhật ký.", entry.GetProperty("reason").GetString());
    }

    [Fact(DisplayName = "ADMIN-23 [Normal] A no-op status change writes no audit entry")]
    public async Task AuditLogs_AfterNoOpChange_StaysEmpty()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}/status", new { isActive = true });

        var response = await Admin().GetAsync($"/api/admin/users/{user.Id}/audit-logs");

        var items = (await ApiEnvelope.DataAsync(response)).GetProperty("items");
        Assert.Empty(items.EnumerateArray());
    }

    [Fact(DisplayName = "ADMIN-24 [Abnormal] A customer cannot read anyone's audit log")]
    public async Task AuditLogs_AsCustomer_IsForbidden()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.Customer)
            .GetAsync($"/api/admin/users/{user.Id}/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Tìm người dùng (dành cho chủ vườn mời nhân viên) =====================

    [Fact(DisplayName = "ADMIN-25 [Normal] A garden owner searches users by email")]
    public async Task SearchUsers_AsGardenOwner_FindsUser()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var term = user.Email[..12];

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .GetAsync($"/api/users/search?q={Uri.EscapeDataString(term)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.EnumerateArray(), u => u.GetProperty("id").GetGuid() == user.Id);
    }

    [Fact(DisplayName = "ADMIN-26 [Boundary] A search term shorter than three characters is rejected")]
    public async Task SearchUsers_TooShortTerm_IsRejected()
    {
        var response = await _fixture.ClientFor(TestRole.GardenOwner).GetAsync("/api/users/search?q=ab");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "ADMIN-27 [Abnormal] A plain customer cannot search users")]
    public async Task SearchUsers_AsCustomer_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync("/api/users/search?q=test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Admin() => _fixture.ClientFor(TestRole.Admin);

    private async Task<int> TokenVersionAsync(Guid userId)
    {
        var response = await Admin().GetAsync($"/api/admin/users/{userId}");
        return (await ApiEnvelope.DataAsync(response)).GetProperty("tokenVersion").GetInt32();
    }
}
