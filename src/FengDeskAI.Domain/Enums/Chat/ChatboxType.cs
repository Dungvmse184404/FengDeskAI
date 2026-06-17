namespace FengDeskAI.Domain.Enums.Chat;

/// <summary>Loại hội thoại.</summary>
public enum ChatboxType
{
    /// <summary>Giữa hai người dùng (customer ↔ garden owner / staff / manager...).</summary>
    Direct = 0,

    /// <summary>Giữa một người dùng và trợ lý AI (RecipientUserId = null).</summary>
    Assistant = 1,
}
