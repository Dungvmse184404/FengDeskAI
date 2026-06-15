using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Chat;

public class ChatMessage : BaseEntity
{
    public Guid ChatboxId { get; set; }
    public Chatbox Chatbox { get; set; } = null!;

    /// <summary>Người gửi message.</summary>
    public Guid SenderUserId { get; set; }

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
