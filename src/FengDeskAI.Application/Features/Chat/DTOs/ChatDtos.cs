using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.Chat.DTOs;

public class ChatParticipantResponse
{
    public Guid? UserId { get; set; }
    public ParticipantType ParticipantType { get; set; }
    public ParticipantRole Role { get; set; }
    public bool IsMuted { get; set; }
    public bool IsHidden { get; set; }
}

public class ChatboxResponse
{
    public Guid Id { get; set; }
    public bool IsGroup { get; set; }
    public string? Title { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ChatParticipantResponse> Participants { get; set; } = new();

    /// <summary>Tin nhắn gần nhất trong phòng.</summary>
    public ChatMessageResponse? LastMessage { get; set; }
}

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public Guid ChatboxId { get; set; }
    public Guid? SenderId { get; set; }
    public MessageSenderType SenderType { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Link ảnh đính kèm (nếu có).</summary>
    public List<string> Images { get; set; } = new();
}

public class CreateGroupRequest
{
    public string? Title { get; set; }
    public List<Guid> MemberUserIds { get; set; } = new();
}

public class AddParticipantRequest
{
    public Guid UserId { get; set; }
}

public class SendMessageRequest
{
    /// <summary>Nội dung văn bản. Có thể null/trống nếu chỉ gửi ảnh.</summary>
    public string? Content { get; set; }

    /// <summary>Link ảnh đã upload (qua endpoint upload ảnh chat). Không gửi nhị phân ở đây.</summary>
    public List<string>? ImageUrls { get; set; }
}

public class ChatboxListResponse
{
    public List<ChatboxResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
