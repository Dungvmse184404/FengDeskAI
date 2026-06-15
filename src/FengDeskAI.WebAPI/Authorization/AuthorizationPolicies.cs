namespace FengDeskAI.WebAPI.Authorization;

/// <summary>
/// Tên policy authorization — tránh magic string trong [Authorize(Policy = ...)].
/// Dùng kèm role tên (Customer/Manager/Staff/Admin) cho JWT claim ClaimTypes.Role.
///
/// Thứ tự quyền (thấp → cao): Customer &lt; Manager &lt; Staff &lt; Admin.
/// Các policy "...OrAbove" gom đúng role đó và mọi role CAO HƠN.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Chỉ Admin.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Staff trở lên — gồm Staff, Admin (KHÔNG gồm Manager vì Manager thấp hơn Staff).</summary>
    public const string StaffOrAbove = nameof(StaffOrAbove);

    /// <summary>Manager trở lên — gồm Manager, Staff, Admin.</summary>
    public const string ManagerOrAbove = nameof(ManagerOrAbove);

    /// <summary>Chỉ Customer.</summary>
    public const string CustomerOnly = nameof(CustomerOnly);
}

public static class Roles
{
    public const string Customer = nameof(Customer);
    public const string Manager = nameof(Manager);
    public const string Staff = nameof(Staff);
    public const string Admin = nameof(Admin);
}
