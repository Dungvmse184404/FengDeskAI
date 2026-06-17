using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.Chat;

/// <summary>
/// Suy ra thông tin bên gửi cho tin nhắn từ role hệ thống (JWT) và email.
/// "GardenOwner" không suy được chỉ từ role nên tạm map theo role hệ thống.
/// </summary>
public static class ChatSenderHelper
{
    public static ChatRole RoleFrom(string? systemRole) => systemRole switch
    {
        "Admin" => ChatRole.Admin,
        "Staff" => ChatRole.Staff,
        "Manager" => ChatRole.Manager,
        _ => ChatRole.Customer,
    };

    /// <summary>Prefix email làm tên hiển thị (dungvu2324@gmail.com → "dungvu2324").</summary>
    public static string? NameFrom(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
