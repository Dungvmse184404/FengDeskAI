using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>
/// Một phòng chat. Thành viên (gồm cả AI) nằm ở <see cref="Participants"/> — không còn cứng
/// sender/recipient. Phòng "riêng" (chỉ 1 user + AiBot) hay "chung" (≥2 người) suy từ participants.
/// </summary>
public class Chatbox : BaseEntity
{
    /// <summary>True = nhóm; false = 1-1. Chỉ mang tính hiển thị/gợi ý.</summary>
    public bool IsGroup { get; set; } = true;

    /// <summary>Tên phòng (nhóm). Null cho phòng 1-1.</summary>
    public string? Title { get; set; }

    /// <summary>Người tạo phòng.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Sản phẩm đang bàn tới (nếu có) — ngữ cảnh cho AI.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Bật bot AI tự trả lời mọi tin của người trong phòng (Phase 3). Mặc định tắt.</summary>
    public bool IsAiEnabled { get; set; }

    public virtual ICollection<ChatboxParticipant> Participants { get; set; } = new List<ChatboxParticipant>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
