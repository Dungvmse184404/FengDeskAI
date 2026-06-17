using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>
/// Một thành viên của phòng chat. <see cref="UserId"/> null khi thành viên là AI
/// (<see cref="ParticipantType.AiBot"/>). Theo dõi đã-đọc qua <see cref="LastReadAt"/>.
/// </summary>
public class ChatboxParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatboxId { get; set; }
    public Chatbox Chatbox { get; set; } = null!;

    /// <summary>Null = AI.</summary>
    public Guid? UserId { get; set; }

    public ParticipantType ParticipantType { get; set; }
    public ParticipantRole Role { get; set; } = ParticipantRole.Member;

    public bool IsMuted { get; set; }
    public bool IsHidden { get; set; }

    /// <summary>Mốc đã đọc gần nhất của thành viên này.</summary>
    public DateTime? LastReadAt { get; set; }
    public DateTime JoinedAt { get; set; }
}
