using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Chốt tự bảo vệ của quản trị người dùng: không được khóa hay hạ quyền Admin HOẠT ĐỘNG CUỐI CÙNG.
///
/// Vì sao phải là unit test chứ không phải integration test: nhánh này chỉ chạy khi
/// <c>CountActiveAdminsAsync() &lt;= 1</c>. Trong bộ integration luôn có ít nhất hai admin
/// (<c>AdminUserSeeder</c> tạo <c>admin@fengdesk.local</c>, cộng admin mẫu của fixture), nên nhánh
/// đó KHÔNG BAO GIỜ chạm tới. Muốn chạm phải hạ hết admin khác — làm vậy sẽ phá fixture dùng chung
/// của cả bộ. Mock repository là cách duy nhất dựng được đúng tình huống "chỉ còn một admin".
///
/// Đây là chốt chặn duy nhất giữa một cú click nhầm và việc cả hệ thống không còn ai quản trị được.
/// </summary>
public class AdminUserGuardTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ---------------- Khóa tài khoản ----------------

    [Fact(DisplayName = "ADMINGUARD-01 [Boundary] The last active admin cannot be locked")]
    public async Task LockUser_LastActiveAdmin_ReturnsConflict()
    {
        var target = NewUser(TargetId, UserRole.Admin);
        var service = BuildService(target, activeAdminCount: 1);

        var result = await service.UpdateStatusAsync(TargetId, ActorId, null, new UpdateUserStatusRequest
        {
            IsActive = false,
            Reason = "Thử khóa admin cuối cùng.",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("cuối cùng", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.True(target.IsActive, "Tài khoản không được đổi trạng thái khi chốt chặn đã từ chối.");
    }

    [Fact(DisplayName = "ADMINGUARD-02 [Normal] An admin can be locked while another active admin remains")]
    public async Task LockUser_WithASecondAdmin_Succeeds()
    {
        var target = NewUser(TargetId, UserRole.Admin);
        var service = BuildService(target, activeAdminCount: 2);

        var result = await service.UpdateStatusAsync(TargetId, ActorId, null, new UpdateUserStatusRequest
        {
            IsActive = false,
        });

        Assert.True(result.IsSuccess);
        Assert.False(target.IsActive);
    }

    [Fact(DisplayName = "ADMINGUARD-03 [Boundary] The last-admin guard does not block locking a non-admin")]
    public async Task LockUser_LastAdminCountButTargetIsCustomer_Succeeds()
    {
        // Chốt chỉ được nhìn vào role của NGƯỜI BỊ KHÓA. Nếu nó chặn theo số lượng admin mà không
        // xét role thì khóa một khách hàng bình thường cũng bị từ chối.
        var target = NewUser(TargetId, UserRole.Customer);
        var service = BuildService(target, activeAdminCount: 1);

        var result = await service.UpdateStatusAsync(TargetId, ActorId, null, new UpdateUserStatusRequest
        {
            IsActive = false,
        });

        Assert.True(result.IsSuccess);
        Assert.False(target.IsActive);
    }

    [Fact(DisplayName = "ADMINGUARD-04 [Abnormal] An admin cannot lock their own account")]
    public async Task LockUser_Self_ReturnsBadRequest()
    {
        var self = NewUser(ActorId, UserRole.Admin);
        var service = BuildService(self, activeAdminCount: 5);

        var result = await service.UpdateStatusAsync(ActorId, ActorId, null, new UpdateUserStatusRequest
        {
            IsActive = false,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.True(self.IsActive);
    }

    [Fact(DisplayName = "ADMINGUARD-05 [Boundary] Unlocking the last admin is always allowed")]
    public async Task UnlockUser_LastAdmin_Succeeds()
    {
        // Chốt chỉ chặn chiều KHÓA. Chặn cả chiều mở là tự khóa mình ra ngoài hệ thống.
        var target = NewUser(TargetId, UserRole.Admin, isActive: false);
        var service = BuildService(target, activeAdminCount: 1);

        var result = await service.UpdateStatusAsync(TargetId, ActorId, null, new UpdateUserStatusRequest
        {
            IsActive = true,
        });

        Assert.True(result.IsSuccess);
        Assert.True(target.IsActive);
    }

    // ---------------- Đổi vai trò ----------------

    [Fact(DisplayName = "ADMINGUARD-06 [Boundary] The last active admin cannot be demoted")]
    public async Task UpdateRoles_DemotingLastAdmin_ReturnsConflict()
    {
        var target = NewUser(TargetId, UserRole.Admin);
        var service = BuildService(target, activeAdminCount: 1);

        var result = await service.UpdateRolesAsync(TargetId, ActorId, null, new UpdateUserRolesRequest
        {
            Roles = new[] { UserRole.Customer },
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(UserRole.Admin, target.Role);
    }

    [Fact(DisplayName = "ADMINGUARD-07 [Normal] An admin can be demoted while another active admin remains")]
    public async Task UpdateRoles_DemotingWithASecondAdmin_Succeeds()
    {
        var target = NewUser(TargetId, UserRole.Admin);
        var service = BuildService(target, activeAdminCount: 2);

        var result = await service.UpdateRolesAsync(TargetId, ActorId, null, new UpdateUserRolesRequest
        {
            Roles = new[] { UserRole.Customer },
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Customer, target.Role);
    }

    [Fact(DisplayName = "ADMINGUARD-08 [Normal] Keeping the admin role on the last admin is allowed")]
    public async Task UpdateRoles_LastAdminKeepsAdminRole_Succeeds()
    {
        // Thêm quyền cho admin cuối cùng KHÔNG được coi là hạ quyền — chốt chỉ chặn khi role mới
        // không còn cờ Admin.
        var target = NewUser(TargetId, UserRole.Admin);
        var service = BuildService(target, activeAdminCount: 1);

        var result = await service.UpdateRolesAsync(TargetId, ActorId, null, new UpdateUserRolesRequest
        {
            Roles = new[] { UserRole.Admin, UserRole.GardenOwner },
        });

        Assert.True(result.IsSuccess);
        Assert.True(target.Role.Has(UserRole.Admin));
        Assert.True(target.Role.Has(UserRole.GardenOwner));
    }

    [Fact(DisplayName = "ADMINGUARD-09 [Abnormal] An admin cannot strip their own admin role")]
    public async Task UpdateRoles_SelfDemotion_ReturnsBadRequest()
    {
        var self = NewUser(ActorId, UserRole.Admin);
        var service = BuildService(self, activeAdminCount: 5);

        var result = await service.UpdateRolesAsync(ActorId, ActorId, null, new UpdateUserRolesRequest
        {
            Roles = new[] { UserRole.Customer },
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(UserRole.Admin, self.Role);
    }

    [Theory(DisplayName = "ADMINGUARD-10 [Abnormal] An invalid role list is rejected")]
    [MemberData(nameof(InvalidRoleLists))]
    public async Task UpdateRoles_InvalidList_ReturnsBadRequest(UserRole[] roles)
    {
        var target = NewUser(TargetId, UserRole.Customer);
        var service = BuildService(target, activeAdminCount: 5);

        var result = await service.UpdateRolesAsync(TargetId, ActorId, null, new UpdateUserRolesRequest
        {
            Roles = roles,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(UserRole.Customer, target.Role);
    }

    public static IEnumerable<object[]> InvalidRoleLists() => new List<object[]>
    {
        // Danh sách rỗng: user không còn vai trò nào thì không đăng nhập làm gì được.
        new object[] { Array.Empty<UserRole>() },
        // None không phải một vai trò.
        new object[] { new[] { UserRole.None } },
        // Giá trị tổ hợp trong MỘT phần tử: phải gửi từng cờ rời, không gửi mask gộp sẵn.
        new object[] { new[] { UserRole.Customer | UserRole.Manager } },
        // Giá trị ngoài miền enum.
        new object[] { new[] { (UserRole)9999 } },
    };

    // ---------------- Helper ----------------

    private static User NewUser(Guid id, UserRole role, bool isActive = true) => new()
    {
        Id = id,
        Email = $"user-{id:N}@fengdesk.test",
        FullName = "Unit Test User",
        PasswordHash = "hash-gia-lap",
        Role = role,
        IsActive = isActive,
        TokenVersion = 0,
    };

    /// <summary>
    /// Dựng service với repository giả. Chỉ mock đúng ba thứ service dùng tới trong các nhánh trên:
    /// tra user, đếm admin đang hoạt động, thu hồi refresh token.
    /// </summary>
    private static AdminUserService BuildService(User target, int activeAdminCount)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        users.Setup(r => r.CountActiveAdminsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(activeAdminCount);

        var refreshTokens = new Mock<IRefreshTokenRepository>();
        refreshTokens
            .Setup(r => r.RevokeAllActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audits = new Mock<IAuthorizationAuditRepository>();

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Users).Returns(users.Object);
        uow.SetupGet(u => u.RefreshTokens).Returns(refreshTokens.Object);
        uow.SetupGet(u => u.AuthorizationAudits).Returns(audits.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new AdminUserService(uow.Object);
    }
}
