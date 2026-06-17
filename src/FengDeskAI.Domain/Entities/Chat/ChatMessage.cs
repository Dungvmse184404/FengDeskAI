using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Domain.Entities.Chat;

public class ChatMessage : BaseEntity
{
    public Guid ChatboxId { get; set; }
    public Chatbox Chatbox { get; set; } = null!;

    /// <summary>Người gửi. Null khi tin nhắn do AI/hệ thống sinh ra.</summary>
    public Guid? SenderUserId { get; set; }

    /// <summary>Vai trò của bên gửi — để AI phân biệt các bên.</summary>
    public ChatRole SenderRole { get; set; }

    /// <summary>Tên hiển thị (prefix email, vd "dungvu2324"). Null cho AI/hệ thống.</summary>
    public string? SenderName { get; set; }

    /// <summary>Nội dung văn bản. Null nếu tin nhắn chỉ gồm ảnh.</summary>
    public string? Content { get; set; }

    /// <summary>True nếu tin nhắn do trợ lý AI sinh ra.</summary>
    public bool IsFromAi { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Ảnh đính kèm — chỉ lưu link, không lưu nhị phân.</summary>
    public virtual ICollection<ChatMessageImage> Images { get; set; } = new List<ChatMessageImage>();
}
