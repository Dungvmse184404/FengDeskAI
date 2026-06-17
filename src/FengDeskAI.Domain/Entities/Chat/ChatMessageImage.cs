using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>
/// Ảnh đính kèm một <see cref="ChatMessage"/>. Chỉ lưu LINK (URL trên storage), KHÔNG lưu nhị phân.
/// </summary>
public class ChatMessageImage : BaseEntity
{
    public Guid ChatMessageId { get; set; }
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }

    public ChatMessage Message { get; set; } = null!;
}
