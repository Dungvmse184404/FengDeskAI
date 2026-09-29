namespace FengDeskAI.WebAPI.Authorization;

/// <summary>
/// Tên policy authorization — tránh magic string trong [Authorize(Policy = ...)].
/// Dùng kèm role tên (Customer/Staff/Manager/Admin) cho JWT claim ClaimTypes.Role.
///
/// Thứ tự quyền (thấp → cao): Customer &lt; Staff &lt; Manager &lt; Admin.
/// Staff là tuyến hỗ trợ trực tiếp cho Customer/garden owner; Manager cao hơn Staff.
/// Các policy "...OrAbove" gom đúng role đó và mọi role CAO HƠN.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Chỉ Admin.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Staff trở lên — gồm Staff, Manager, Admin.</summary>
    public const string StaffOrAbove = nameof(StaffOrAbove);

    /// <summary>Manager trở lên — gồm Manager, Admin (KHÔNG gồm Staff vì Staff thấp hơn Manager).</summary>
    public const string ManagerOrAbove = nameof(ManagerOrAbove);

    /// <summary>Chỉ Customer.</summary>
    public const string CustomerOnly = nameof(CustomerOnly);

    /// <summary>Người bán (GardenOwner) hoặc người có quyền vận hành toàn sàn (Manager/Admin).</summary>
    public const string GardenOwnerOrAbove = nameof(GardenOwnerOrAbove);
}

public static class Roles
{
    public const string Customer = nameof(Customer);
    public const string Manager = nameof(Manager);
    public const string Staff = nameof(Staff);
    public const string Admin = nameof(Admin);
    public const string GardenOwner = nameof(GardenOwner);
}

/// <summary>
/// Các nhóm quyền dùng trong code (ngoài attribute policy).
/// Manager được phép vận hành sàn như Admin, nhưng không được xem là Admin để truy cập
/// các chức năng quản trị tài khoản/phân quyền bảo vệ bởi <see cref="AuthorizationPolicies.AdminOnly"/>.
/// </summary>
public static class RoleChecks
{
    public static bool CanOperateAsAdmin(this System.Security.Claims.ClaimsPrincipal user)
        => user.IsInRole(Roles.Manager) || user.IsInRole(Roles.Admin);
}
