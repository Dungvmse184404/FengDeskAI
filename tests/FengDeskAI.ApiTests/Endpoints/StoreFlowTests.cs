using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 5/6 — Cửa hàng: tự đăng ký cửa hàng, địa chỉ giao hàng, đồng sở hữu, vòng đời lời mời nhân
/// viên, và thống kê.
///
/// Bẫy phải biết trước khi đọc: <c>POST /api/stores</c> và <c>POST /api/stores/{id}/owners</c> gọi
/// <c>GrantGardenOwnerRoleAsync</c> — user CHƯA có role GardenOwner sẽ bị tăng
/// <c>TokenVersion</c> và thu hồi toàn bộ refresh token, tức access token đang cầm chết ngay lập
/// tức. Nếu nhắm vào user mẫu dùng chung của fixture thì mọi ca test chạy sau đó sẽ 401 theo, và
/// lỗi phụ thuộc thứ tự chạy nên rất khó lần. Vì thế mọi ca ở đây dùng
/// <see cref="ScenarioUsers"/> — user dùng một lần.
///
/// Vòng đời lời mời nhân viên đang được khẳng định:
/// <code>
/// Pending  → Accepted  (người được mời)
/// Pending  → Rejected  (người được mời)
/// Pending  → Revoked   (chủ cửa hàng)
/// Accepted → Revoked   (chủ cửa hàng)
/// Rejected / Revoked   → không quay lại được
/// </code>
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class StoreFlowTests
{
    /// <summary>Số di động hợp lệ với nhà vận chuyển — hotline 1900 hay số cố định đều bị từ chối.</summary>
    private const string ValidHotline = "0901234567";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StoreFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Tự đăng ký cửa hàng =====================

    [Fact(DisplayName = "STORE-01 [Normal] Creating a store makes the caller its primary owner")]
    public async Task CreateStore_AsCustomer_MakesCallerPrimaryOwner()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var response = await client.PostAsJsonAsync("/api/stores", new
        {
            name = $"Vườn {Guid.NewGuid():N}"[..18],
            description = "Cửa hàng dựng trong test.",
            hotline = ValidHotline,
            openingHours = "08:00 - 21:00",
        });

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Contains(data.GetProperty("owners").EnumerateArray(),
            o => o.GetProperty("ownerUserId").GetGuid() == user.Id && o.GetProperty("isPrimary").GetBoolean());
    }

    [Fact(DisplayName = "STORE-02 [Normal] Creating a store grants the caller the garden-owner role")]
    public async Task CreateStore_GrantsGardenOwnerRole_AndInvalidatesOldToken()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        await CreateStoreAsync(user);

        // Token cũ chết cùng TokenVersion; đăng nhập lại thì role mới đã có hiệu lực và
        // endpoint chỉ dành cho GardenOwner trở lên phải đi lọt.
        var refreshed = user with { AccessToken = await ScenarioUsers.LoginAsync(_fixture, user) };
        var response = await ScenarioUsers.ClientFor(_fixture, refreshed)
            .PostAsJsonAsync("/api/tags", new { name = $"tag-{Guid.NewGuid():N}"[..14] });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-03 [Abnormal] A store with an invalid hotline is rejected")]
    public async Task CreateStore_InvalidHotline_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/stores", new
        {
            name = "Vườn số xấu",
            hotline = "123",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("hotline", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "STORE-04 [Abnormal] A store with a blank name is rejected")]
    public async Task CreateStore_BlankName_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/stores", new { name = "  ", hotline = ValidHotline });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-05 [Normal] A new store appears in the owner's own store list")]
    public async Task ListMyStores_AfterCreate_ContainsStore()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/stores/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.EnumerateArray(), s => s.GetProperty("id").GetGuid() == storeId);
    }

    // ===================== Sửa / xóa =====================

    [Fact(DisplayName = "STORE-06 [Normal] The owner updates their store")]
    public async Task UpdateStore_AsOwner_PersistsChange()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PutAsJsonAsync($"/api/stores/{storeId}", new
        {
            name = "Vườn đã đổi tên",
            hotline = ValidHotline,
            isActive = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Vườn đã đổi tên", data.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "STORE-07 [Abnormal] A stranger cannot update someone else's store")]
    public async Task UpdateStore_AsStranger_IsForbidden()
    {
        var (_, storeId) = await OwnerWithStoreAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger).PutAsJsonAsync($"/api/stores/{storeId}", new
        {
            name = "Chiếm cửa hàng",
            hotline = ValidHotline,
            isActive = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("quyền", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "STORE-08 [Abnormal] Updating an unknown store returns 404")]
    public async Task UpdateStore_UnknownId_ReturnsNotFound()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PutAsJsonAsync($"/api/stores/{Guid.NewGuid()}", new { name = "Không có", hotline = ValidHotline, isActive = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Địa chỉ =====================

    [Fact(DisplayName = "STORE-09 [Normal] The owner adds a shipping address to their store")]
    public async Task AddAddress_AsOwner_Succeeds()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync($"/api/stores/{storeId}/address", new
        {
            wardId,
            streetAddress = "12 Đường Kiểm Thử",
            senderName = "Vườn kiểm thử",
            senderPhone = ValidHotline,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-10 [Abnormal] Adding a second address to the same store returns 409")]
    public async Task AddAddress_Twice_ReturnsConflict()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var wardId = await AnyWardIdAsync();
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var first = await client.PostAsJsonAsync($"/api/stores/{storeId}/address",
            new { wardId, streetAddress = "12 Đường Kiểm Thử" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/stores/{storeId}/address",
            new { wardId, streetAddress = "34 Đường Khác" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "STORE-11 [Abnormal] An address with an unknown ward is rejected")]
    public async Task AddAddress_UnknownWard_IsRejected()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync($"/api/stores/{storeId}/address",
            new { wardId = Guid.NewGuid(), streetAddress = "12 Đường Kiểm Thử" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-12 [Abnormal] Updating an address before one exists returns 404")]
    public async Task UpdateAddress_BeforeAdd_ReturnsNotFound()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PutAsJsonAsync($"/api/stores/{storeId}/address",
            new { wardId, streetAddress = "12 Đường Kiểm Thử" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Đồng sở hữu =====================

    [Fact(DisplayName = "STORE-13 [Normal] The primary owner adds a co-owner")]
    public async Task AddOwner_AsPrimaryOwner_Succeeds()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var coOwner = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync($"/api/stores/{storeId}/owners", new { ownerUserId = coOwner.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.False(data.GetProperty("isPrimary").GetBoolean(),
            "Đồng sở hữu thêm sau không được là chủ chính.");
    }

    [Fact(DisplayName = "STORE-14 [Abnormal] Adding the same co-owner twice returns 409")]
    public async Task AddOwner_Twice_ReturnsConflict()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var coOwner = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        await client.PostAsJsonAsync($"/api/stores/{storeId}/owners", new { ownerUserId = coOwner.Id });
        var second = await client.PostAsJsonAsync($"/api/stores/{storeId}/owners", new { ownerUserId = coOwner.Id });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "STORE-15 [Abnormal] The last primary owner cannot be removed")]
    public async Task RemoveOwner_LastPrimaryOwner_IsRejected()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .DeleteAsync($"/api/stores/{storeId}/owners/{user.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("chủ sở hữu chính", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "STORE-16 [Normal] A co-owner can be removed again")]
    public async Task RemoveOwner_CoOwner_Succeeds()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var coOwner = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);
        await client.PostAsJsonAsync($"/api/stores/{storeId}/owners", new { ownerUserId = coOwner.Id });

        var response = await client.DeleteAsync($"/api/stores/{storeId}/owners/{coOwner.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===================== Lời mời nhân viên =====================

    [Fact(DisplayName = "STORE-17 [Normal] The owner invites a staff member and the invitation is pending")]
    public async Task InviteStaff_AsOwner_CreatesPendingInvitation()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var staff = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = staff.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Pending", data.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "STORE-18 [Abnormal] Inviting the store owner as staff is rejected")]
    public async Task InviteStaff_TheOwnerThemselves_IsRejected()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = user.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-19 [Abnormal] Inviting the same person while a request is pending returns 409")]
    public async Task InviteStaff_Twice_ReturnsConflict()
    {
        var (user, storeId) = await OwnerWithStoreAsync();
        var staff = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        await client.PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = staff.Id });
        var second = await client.PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = staff.Id });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "STORE-20 [Abnormal] Inviting an unknown user is rejected")]
    public async Task InviteStaff_UnknownUser_IsRejected()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-21 [Normal] The invitee sees the invitation in their own list")]
    public async Task ListMyInvitations_AsInvitee_ContainsInvitation()
    {
        var (assignmentId, staff, _) = await PendingInvitationAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, staff).GetAsync("/api/stores/staff/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.EnumerateArray(), i => i.GetProperty("id").GetGuid() == assignmentId);
    }

    [Fact(DisplayName = "STORE-22 [Normal] The invitee accepts and becomes accepted staff")]
    public async Task AcceptInvitation_AsInvitee_MovesToAccepted()
    {
        var (assignmentId, staff, _) = await PendingInvitationAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, staff)
            .PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Accepted", data.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "STORE-23 [Abnormal] Responding to the same invitation twice returns 409")]
    public async Task AcceptInvitation_Twice_ReturnsConflict()
    {
        var (assignmentId, staff, _) = await PendingInvitationAsync();
        var client = ScenarioUsers.ClientFor(_fixture, staff);
        await client.PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        var second = await client.PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "STORE-24 [Normal] The invitee rejects the invitation")]
    public async Task RejectInvitation_AsInvitee_Succeeds()
    {
        var (assignmentId, staff, _) = await PendingInvitationAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, staff)
            .PostAsync($"/api/stores/staff/{assignmentId}/reject", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-25 [Abnormal] Someone else cannot answer an invitation addressed to another user")]
    public async Task AcceptInvitation_AsStranger_ReturnsNotFound()
    {
        var (assignmentId, _, _) = await PendingInvitationAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-26 [Normal] The owner revokes an accepted staff assignment")]
    public async Task UnassignStaff_AfterAccept_Succeeds()
    {
        var (assignmentId, staff, owner) = await PendingInvitationAsync();
        var storeId = await StoreIdOfAsync(owner);
        await ScenarioUsers.ClientFor(_fixture, staff).PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        var response = await ScenarioUsers.ClientFor(_fixture, owner)
            .DeleteAsync($"/api/stores/{storeId}/staff/{assignmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-27 [Abnormal] Revoking an already-rejected assignment returns 404")]
    public async Task UnassignStaff_AfterReject_ReturnsNotFound()
    {
        var (assignmentId, staff, owner) = await PendingInvitationAsync();
        var storeId = await StoreIdOfAsync(owner);
        await ScenarioUsers.ClientFor(_fixture, staff).PostAsync($"/api/stores/staff/{assignmentId}/reject", null);

        var response = await ScenarioUsers.ClientFor(_fixture, owner)
            .DeleteAsync($"/api/stores/{storeId}/staff/{assignmentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-28 [Normal] Membership reports accepted staff as staff, not owner")]
    public async Task Membership_ForAcceptedStaff_ReportsStaff()
    {
        var (assignmentId, staff, owner) = await PendingInvitationAsync();
        var storeId = await StoreIdOfAsync(owner);
        await ScenarioUsers.ClientFor(_fixture, staff).PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        var response = await ScenarioUsers.ClientFor(_fixture, staff).GetAsync($"/api/stores/{storeId}/membership");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.True(data.GetProperty("isStaff").GetBoolean());
        Assert.False(data.GetProperty("isOwner").GetBoolean());
        Assert.True(data.GetProperty("canManage").GetBoolean());
    }

    // ===================== Thống kê =====================

    [Fact(DisplayName = "STORE-29 [Normal] A brand-new store reports zeroed statistics")]
    public async Task Statistics_NewStore_ReturnsZeroes()
    {
        var (user, storeId) = await OwnerWithStoreAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync($"/api/stores/{storeId}/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(0m, data.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(0, data.GetProperty("totalDeliveries").GetInt32());
        Assert.Equal(0, data.GetProperty("productCount").GetInt32());
    }

    [Fact(DisplayName = "STORE-30 [Normal] A delivered order shows up in the store statistics")]
    public async Task Statistics_AfterDeliveredOrder_CountsRevenue()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .GetAsync($"/api/stores/{order.StoreId}/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.True(data.GetProperty("totalRevenue").GetDecimal() > 0m,
            "Đơn đã giao phải được tính vào doanh thu.");
        Assert.True(data.GetProperty("totalDeliveries").GetInt32() >= 1);
    }

    [Fact(DisplayName = "STORE-31 [Abnormal] A stranger cannot read another store's statistics")]
    public async Task Statistics_AsStranger_IsForbidden()
    {
        var (_, storeId) = await OwnerWithStoreAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .GetAsync($"/api/stores/{storeId}/statistics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "STORE-32 [Abnormal] Accepted staff still cannot read the store statistics")]
    public async Task Statistics_AsAcceptedStaff_IsForbidden()
    {
        var (assignmentId, staff, owner) = await PendingInvitationAsync();
        var storeId = await StoreIdOfAsync(owner);
        await ScenarioUsers.ClientFor(_fixture, staff).PostAsync($"/api/stores/staff/{assignmentId}/accept", null);

        var response = await ScenarioUsers.ClientFor(_fixture, staff).GetAsync($"/api/stores/{storeId}/statistics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Helper =====================

    /// <summary>
    /// Tạo cửa hàng rồi trả về user với TOKEN MỚI: bước tạo cửa hàng cấp role GardenOwner nên đã
    /// làm token cũ hết hiệu lực (xem ghi chú ở đầu file).
    /// </summary>
    private async Task<(ThrowawayUser Owner, Guid StoreId)> OwnerWithStoreAsync()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var storeId = await CreateStoreAsync(user);
        var refreshed = user with { AccessToken = await ScenarioUsers.LoginAsync(_fixture, user) };
        return (refreshed, storeId);
    }

    private async Task<Guid> CreateStoreAsync(ThrowawayUser user)
    {
        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/stores", new
        {
            name = $"Vườn {Guid.NewGuid():N}"[..18],
            hotline = ValidHotline,
            openingHours = "08:00 - 21:00",
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo cửa hàng"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> StoreIdOfAsync(ThrowawayUser owner)
    {
        var response = await ScenarioUsers.ClientFor(_fixture, owner).GetAsync("/api/stores/mine");
        var data = await ApiEnvelope.DataAsync(response);
        return data.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    /// <summary>Một lời mời đang chờ, kèm cả người được mời lẫn chủ cửa hàng đã có token hợp lệ.</summary>
    private async Task<(Guid AssignmentId, ThrowawayUser Staff, ThrowawayUser Owner)> PendingInvitationAsync()
    {
        var (owner, storeId) = await OwnerWithStoreAsync();
        var staff = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, owner)
            .PostAsJsonAsync($"/api/stores/{storeId}/staff", new { staffId = staff.Id });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "mời nhân viên"));

        var assignmentId = (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
        return (assignmentId, staff, owner);
    }

    /// <summary>
    /// Một phường/xã bất kỳ do <c>GeographySeeder</c> tạo. Đọc thẳng DB vì đây là DỮ LIỆU NỀN —
    /// đi qua endpoint tra cứu sẽ biến mọi ca test địa chỉ thành ca test của endpoint đó.
    /// </summary>
    private async Task<Guid> AnyWardIdAsync()
    {
        var wardId = Guid.Empty;
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            wardId = await db.Set<Ward>().OrderBy(w => w.Name).Select(w => w.Id).FirstAsync();
        });
        return wardId;
    }
}
