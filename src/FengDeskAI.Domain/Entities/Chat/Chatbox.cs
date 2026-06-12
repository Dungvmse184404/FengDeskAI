using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>1-1 conversation giữa 2 người dùng.</summary>
public class Chatbox : BaseEntity
{
    public Guid SenderUserId { get; set; }
    public Guid RecipientUserId { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
