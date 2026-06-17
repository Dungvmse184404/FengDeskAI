using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Domain.Entities.Chat;

public class ChatMessage : BaseEntity
{
    public Guid ChatboxId { get; set; }
    public Chatbox Chatbox { get; set; } = null!;

    /// <summary>Người gửi. Null khi do AI/hệ thống sinh ra.</summary>
    public Guid? SenderId { get; set; }

    /// <summary>Nguồn tin (User/AiBot/System) — để FE render.</summary>
    public MessageSenderType SenderType { get; set; }

    /// <summary>Tên hiển thị (prefix email) — snapshot để AI phân biệt người cùng vai. Null cho AI.</summary>
    public string? SenderName { get; set; }

    /// <summary>Nội dung văn bản. Null nếu chỉ gồm ảnh.</summary>
    public string? Content { get; set; }

    /// <summary>Ảnh đính kèm — chỉ lưu link.</summary>
    public virtual ICollection<ChatMessageImage> Images { get; set; } = new List<ChatMessageImage>();
}
