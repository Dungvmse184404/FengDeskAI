namespace FengDeskAI.Domain.Enums.Chat;

/// <summary>
/// Vai trò của người/agent gửi một tin nhắn — giúp AI phân biệt các bên trong hội thoại.
/// Customer/GardenOwner/Staff/Manager/Admin là người; Assistant/System do hệ thống sinh.
/// </summary>
public enum ChatRole
{
    Customer = 0,
    GardenOwner = 1,
    Staff = 2,
    Manager = 3,
    Admin = 4,
    Assistant = 5,
    System = 6,
}
