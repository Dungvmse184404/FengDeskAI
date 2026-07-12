namespace FengDeskAI.Domain.Enums.Chat;

/// <summary>Phân loại một thành viên trong phòng chat. AiBot = trợ lý AI (user_id null).</summary>
public enum ParticipantType
{
    Customer = 0,
    Staff = 1,
    Manager = 2,
    Admin = 3,

    /// <summary>Garden owner/staff trả lời trong phòng hỗ trợ CỦA MỘT SHOP (Chatbox.GardenStoreId != null).
    /// Khác Staff/Manager/Admin (platform CSKH) — không được tự join phòng hỗ trợ platform và ngược lại.</summary>
    Vendor = 4,

    AiBot = 9,
}
