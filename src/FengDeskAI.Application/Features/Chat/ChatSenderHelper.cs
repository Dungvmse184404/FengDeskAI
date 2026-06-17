using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.Chat;

/// <summary>Suy ParticipantType + tên hiển thị từ role hệ thống (JWT) và email.</summary>
public static class ChatSenderHelper
{
    public static ParticipantType TypeFrom(string? systemRole) => systemRole switch
    {
        "Admin" => ParticipantType.Admin,
        "Staff" => ParticipantType.Staff,
        "Manager" => ParticipantType.Manager,
        _ => ParticipantType.Customer,
    };

    /// <summary>Prefix email làm tên hiển thị (dungvu2324@gmail.com → "dungvu2324").</summary>
    public static string? NameFrom(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
