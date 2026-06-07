namespace FengDeskAI.WebAPI.Authorization;

/// <summary>
/// Tên policy authorization — tránh magic string trong [Authorize(Policy = ...)].
/// Dùng kèm role tên (Customer/Manager/Staff/Admin) cho JWT claim ClaimTypes.Role.
/// </summary>
public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ManagerOrAdmin = nameof(ManagerOrAdmin);
    public const string StaffOrAbove = nameof(StaffOrAbove);
    public const string CustomerOnly = nameof(CustomerOnly);
}

public static class Roles
{
    public const string Customer = nameof(Customer);
    public const string Manager = nameof(Manager);
    public const string Staff = nameof(Staff);
    public const string Admin = nameof(Admin);
}
