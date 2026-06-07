namespace FengDeskAI.Domain.Enums;

/// <summary>
/// Vai trò người dùng — bit-mask. Một user có thể có nhiều role cùng lúc
/// (ví dụ vừa Customer vừa Staff).
///
/// Giá trị phải là lũy thừa của 2 để bit không xung đột:
///   Customer | Manager = 3  (cả 2 flag)
///   Manager | Staff   = 6  (cả 2 flag)
/// Nếu dùng 1/2/3/4 thì 3 = Customer|Manager sẽ trùng với "Staff = 3" → bug.
/// </summary>
[Flags]
public enum UserRole
{
    None = 0,
    Customer = 1 << 0, // 1
    Manager  = 1 << 1, // 2
    Staff    = 1 << 2, // 4
    Admin    = 1 << 3, // 8
}

public static class UserRoleExtensions
{
    public static bool Has(this UserRole role, UserRole flag) => (role & flag) == flag && flag != UserRole.None;
    public static UserRole Add(this UserRole role, UserRole flag) => role | flag;
    public static UserRole Remove(this UserRole role, UserRole flag) => role & ~flag;

    public static IEnumerable<UserRole> ToFlagList(this UserRole role)
    {
        foreach (UserRole flag in Enum.GetValues<UserRole>())
        {
            if (flag != UserRole.None && role.HasFlag(flag))
                yield return flag;
        }
    }
}
