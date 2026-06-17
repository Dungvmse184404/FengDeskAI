namespace FengDeskAI.Domain.Enums.Chat;

/// <summary>Phân loại một thành viên trong phòng chat. AiBot = trợ lý AI (user_id null).</summary>
public enum ParticipantType
{
    Customer = 0,
    Staff = 1,
    Manager = 2,
    Admin = 3,
    AiBot = 9,
}
