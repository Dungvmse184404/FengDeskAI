using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>
/// Một hội thoại. <see cref="ChatboxType.Direct"/> = giữa hai người dùng;
/// <see cref="ChatboxType.Assistant"/> = giữa một người dùng và AI (RecipientUserId = null).
/// </summary>
public class Chatbox : BaseEntity
{
    public ChatboxType Type { get; set; } = ChatboxType.Direct;

    /// <summary>Người khởi tạo hội thoại.</summary>
    public Guid SenderUserId { get; set; }

    /// <summary>Người còn lại. Null khi là hội thoại với AI.</summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>Sản phẩm đang được bàn tới (nếu có) — ngữ cảnh để AI hỗ trợ.</summary>
    public Guid? ProductId { get; set; }

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
